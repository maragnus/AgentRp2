#pragma warning disable OPENAI001

using System.ClientModel;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Session;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

namespace AgentRp.Services;

public sealed record ModelGenerationRequest(
    AiProvider Provider,
    AiProviderModel Model,
    ModelGenerationCapabilities Capabilities,
    ModelTuningStepState Tuning,
    string SystemPrompt,
    string UserPrompt,
    string OperationName);

public sealed record ResponseImageInput(byte[] Bytes, string ContentType);

public sealed record ResponseImageGenerationRequest(
    AiProvider Provider,
    AiProviderModel Model,
    ModelGenerationCapabilities Capabilities,
    string Prompt,
    string Size,
    string Quality,
    string ReferenceFidelity,
    IReadOnlyList<ResponseImageInput> ReferenceImages,
    string OperationName);

public record ModelTextCompletion(string Text, int InputTokens, int OutputTokens, string ResponseId);

public sealed record ModelStructuredCompletion<T>(T Value, string Text, int InputTokens, int OutputTokens, string ResponseId)
    : ModelTextCompletion(Text, InputTokens, OutputTokens, ResponseId);

public sealed record ResponseImageStreamingUpdate(byte[]? ImageBytes, string ContentType, string? RevisedPrompt, int InputTokens, int OutputTokens, string ResponseId, bool Completed);

public enum ModelAssistantInputKind
{
    UserMessage,
    FunctionCallOutput
}

public enum ModelAssistantStreamingUpdateKind
{
    TextDelta,
    ToolCall,
    Completed
}

public sealed record ModelAssistantInput(ModelAssistantInputKind Kind, string Content, string ToolCallId = "");

public sealed record ModelAssistantTool(string Name, string Description, JsonObject Parameters);

public sealed record ModelAssistantRequest(
    AiProvider Provider,
    AiProviderModel Model,
    ModelGenerationCapabilities Capabilities,
    ModelTuningStepState Tuning,
    string Instructions,
    string ConversationId,
    IReadOnlyList<ModelAssistantInput> Inputs,
    IReadOnlyList<ModelAssistantTool> Tools,
    string OperationName);

public sealed record ModelAssistantStreamingUpdate(
    ModelAssistantStreamingUpdateKind Kind,
    string TextDelta = "",
    string ToolCallId = "",
    string ToolName = "",
    string ToolArgumentsJson = "",
    string ResponseId = "",
    string ConversationId = "",
    int InputTokens = 0,
    int OutputTokens = 0);

public interface IModelGenerationClient
{
    Task<ModelStructuredCompletion<T>> GenerateStructuredAsync<T>(ModelGenerationRequest request, CancellationToken cancellationToken = default);
    Task<ModelTextCompletion> GenerateTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default);
    Task<ModelTextCompletion> GenerateStreamingTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default);
    Task<string> CreateAssistantConversationAsync(AiProvider provider, AiProviderModel model, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ModelAssistantStreamingUpdate> GenerateAssistantStreamingAsync(ModelAssistantRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ResponseImageStreamingUpdate> GenerateStreamingImageAsync(ResponseImageGenerationRequest request, CancellationToken cancellationToken = default);
}

public sealed class OpenAiModelGenerationClient(IModelClientFactory clientFactory) : IModelGenerationClient
{
    public async Task<ModelStructuredCompletion<T>> GenerateStructuredAsync<T>(ModelGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateStructuredText)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not have structured Responses output enabled.");

        try
        {
            var response = await clientFactory.GetChatClient(request.Provider, request.Model).GetResponseAsync<T>(
                BuildMessages(request),
                BuildChatOptions(request),
                cancellationToken: cancellationToken);
            var usage = response.Usage;
            return new(
                response.Result,
                response.Text,
                ToInt(usage?.InputTokenCount),
                ToInt(usage?.OutputTokenCount),
                response.ResponseId ?? "");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException($"{request.OperationName} failed while requesting typed structured output from {request.Provider.Name} with '{request.Model.Id}': {exception.Message}", exception);
        }
    }

    public async Task<ModelTextCompletion> GenerateTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateText)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not support text input and output.");

        var response = await clientFactory.GetChatClient(request.Provider, request.Model).GetResponseAsync(
            BuildMessages(request),
            BuildChatOptions(request),
            cancellationToken);
        return ToCompletion(response);
    }

    public async Task<ModelTextCompletion> GenerateStreamingTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateStreamingText)
            return await GenerateTextAsync(request, cancellationToken);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in clientFactory.GetChatClient(request.Provider, request.Model).GetStreamingResponseAsync(
            BuildMessages(request),
            BuildChatOptions(request),
            cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            updates.Add(update);
        }

        return ToCompletion(await EnumerateUpdates(updates, cancellationToken).ToChatResponseAsync(cancellationToken));
    }

    public async Task<string> CreateAssistantConversationAsync(AiProvider provider, AiProviderModel model, CancellationToken cancellationToken = default)
    {
        var result = await clientFactory.GetConversationClient(provider, model).CreateConversationAsync(
            BinaryContent.Create(BinaryData.FromString("{}")),
            null);
        var json = JsonNode.Parse(result.GetRawResponse().Content.ToString());
        return json?["id"]?.GetValue<string>() ?? "";
    }

    public async IAsyncEnumerable<ModelAssistantStreamingUpdate> GenerateAssistantStreamingAsync(
        ModelAssistantRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateStreamingText || !request.Capabilities.Tools)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' must support streaming text and tools.");

        await foreach (var update in clientFactory.GetResponsesClient(request.Provider, request.Model).CreateResponseStreamingAsync(BuildAssistantOptions(request), cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (update is StreamingResponseOutputTextDeltaUpdate text)
            {
                yield return new(ModelAssistantStreamingUpdateKind.TextDelta, TextDelta: text.Delta);
            }
            else if (update is StreamingResponseOutputItemDoneUpdate done && done.Item is FunctionCallResponseItem functionCall)
            {
                yield return new(
                    ModelAssistantStreamingUpdateKind.ToolCall,
                    ToolCallId: functionCall.CallId,
                    ToolName: functionCall.FunctionName,
                    ToolArgumentsJson: functionCall.FunctionArguments.ToString());
            }
            else if (update is StreamingResponseCompletedUpdate completed)
            {
                var usage = completed.Response.Usage;
                yield return new(
                    ModelAssistantStreamingUpdateKind.Completed,
                    ResponseId: completed.Response.Id,
                    ConversationId: completed.Response.ConversationOptions?.ConversationId ?? request.ConversationId,
                    InputTokens: usage?.InputTokenCount ?? 0,
                    OutputTokens: usage?.OutputTokenCount ?? 0);
            }
            else if (update is StreamingResponseFailedUpdate failed)
            {
                throw new InvalidOperationException($"{request.OperationName} failed because {request.Provider.Name} returned a failed Responses assistant stream: {failed.Response?.Error?.Message ?? "No failure detail was provided."}");
            }
            else if (update is StreamingResponseErrorUpdate error)
            {
                throw new InvalidOperationException($"{request.OperationName} failed because {request.Provider.Name} returned a Responses assistant stream error: {error.Message}");
            }
        }
    }

    public async IAsyncEnumerable<ResponseImageStreamingUpdate> GenerateStreamingImageAsync(
        ResponseImageGenerationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateImage)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not have Responses image output enabled.");

        await foreach (var update in clientFactory.GetResponsesClient(request.Provider, request.Model).CreateResponseStreamingAsync(BuildImageOptions(request), cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (update is StreamingResponseImageGenerationCallPartialImageUpdate partial && partial.PartialImageBytes is not null)
            {
                yield return new(partial.PartialImageBytes.ToArray(), "image/png", null, 0, 0, "", false);
            }
            else if (update is StreamingResponseCompletedUpdate completed)
            {
                var image = completed.Response.OutputItems.OfType<ImageGenerationCallResponseItem>().FirstOrDefault();
                var usage = completed.Response.Usage;
                yield return new(
                    image?.ImageResultBytes?.ToArray(),
                    ContentTypeFor(image?.OutputFormat),
                    image?.RevisedPrompt,
                    usage?.InputTokenCount ?? 0,
                    usage?.OutputTokenCount ?? 0,
                    completed.Response.Id,
                    true);
            }
            else if (update is StreamingResponseFailedUpdate failed)
            {
                throw new InvalidOperationException($"{request.OperationName} failed because {request.Provider.Name} returned a failed Responses image stream: {failed.Response?.Error?.Message ?? "No failure detail was provided."}");
            }
            else if (update is StreamingResponseErrorUpdate error)
            {
                throw new InvalidOperationException($"{request.OperationName} failed because {request.Provider.Name} returned a Responses image stream error: {error.Message}");
            }
        }
    }

    static IReadOnlyList<ChatMessage> BuildMessages(ModelGenerationRequest request) =>
    [
        new(ChatRole.System, request.SystemPrompt),
        new(ChatRole.User, request.UserPrompt)
    ];

    static ChatOptions BuildChatOptions(ModelGenerationRequest request)
    {
        var tuning = TextModelTuningCatalog.Filter(request.Tuning, request.Capabilities);
        return new()
        {
            ModelId = request.Model.Id,
            Temperature = tuning.Temperature,
            TopP = tuning.TopP,
            MaxOutputTokens = tuning.MaxOutputTokenCount
        };
    }

    static CreateResponseOptions BuildImageOptions(ResponseImageGenerationRequest request)
    {
        var options = new CreateResponseOptions
        {
            Model = request.Model.Id,
            Instructions = "Generate the requested image through the Responses image generation tool.",
            StoredOutputEnabled = false,
            ToolChoice = ResponseToolChoice.CreateRequiredChoice()
        };
        var imageGenerationModel = string.IsNullOrWhiteSpace(request.Capabilities.ImageGenerationModel)
            ? request.Model.Id
            : request.Capabilities.ImageGenerationModel;
        options.Tools.Add(ResponseTool.CreateImageGenerationTool(
            imageGenerationModel,
            QualityFor(request.Quality),
            SizeFor(request.Size),
            ImageGenerationToolOutputFileFormat.Png,
            null,
            null,
            null,
            FidelityFor(request.ReferenceFidelity),
            null,
            1,
            ImageGenerationToolAction.Generate));

        if (request.ReferenceImages.Count == 0)
        {
            options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.Prompt));
            return options;
        }

        var parts = new List<ResponseContentPart> { ResponseContentPart.CreateInputTextPart(request.Prompt) };
        foreach (var image in request.ReferenceImages)
            parts.Add(ResponseContentPart.CreateInputImagePart(BinaryData.FromBytes(image.Bytes), ResponseImageDetailLevel.High));

        options.InputItems.Add(ResponseItem.CreateUserMessageItem(parts));
        return options;
    }

    static CreateResponseOptions BuildAssistantOptions(ModelAssistantRequest request)
    {
        var tuning = TextModelTuningCatalog.Filter(request.Tuning, request.Capabilities);
        var options = new CreateResponseOptions
        {
            Model = request.Model.Id,
            Instructions = request.Instructions,
            ConversationOptions = string.IsNullOrWhiteSpace(request.ConversationId) ? null : new(request.ConversationId),
            StoredOutputEnabled = true,
            StreamingEnabled = true,
            ParallelToolCallsEnabled = false,
            ToolChoice = ResponseToolChoice.CreateAutoChoice(),
            Temperature = tuning.Temperature,
            TopP = tuning.TopP,
            MaxOutputTokenCount = tuning.MaxOutputTokenCount
        };

        foreach (var tool in request.Tools)
            options.Tools.Add(ResponseTool.CreateFunctionTool(
                tool.Name,
                BinaryData.FromString(tool.Parameters.ToJsonString(AppJsonSerializerOptions.Web)),
                false,
                tool.Description));

        foreach (var input in request.Inputs)
        {
            if (input.Kind == ModelAssistantInputKind.UserMessage)
                options.InputItems.Add(ResponseItem.CreateUserMessageItem(input.Content));
            else
                options.InputItems.Add(ResponseItem.CreateFunctionCallOutputItem(input.ToolCallId, input.Content));
        }

        return options;
    }

    static ModelTextCompletion ToCompletion(ChatResponse response)
    {
        var usage = response.Usage;
        return new(
            response.Text,
            ToInt(usage?.InputTokenCount),
            ToInt(usage?.OutputTokenCount),
            response.ResponseId ?? "");
    }

    static async IAsyncEnumerable<ChatResponseUpdate> EnumerateUpdates(
        IReadOnlyList<ChatResponseUpdate> updates,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
            await Task.Yield();
        }
    }

    static int ToInt(long? value) => value is null or > int.MaxValue ? 0 : (int)value.Value;

    static ImageGenerationToolQuality QualityFor(string value) => value.ToLowerInvariant() switch
    {
        "high" => ImageGenerationToolQuality.High,
        "medium" => ImageGenerationToolQuality.Medium,
        "low" => ImageGenerationToolQuality.Low,
        _ => ImageGenerationToolQuality.Auto
    };

    static ImageGenerationToolSize SizeFor(string value) => value switch
    {
        "1024x1536" => ImageGenerationToolSize.W1024xH1536,
        "1536x1024" => ImageGenerationToolSize.W1536xH1024,
        "1024x1024" => ImageGenerationToolSize.W1024xH1024,
        _ => ImageGenerationToolSize.Auto
    };

    static ImageGenerationToolInputFidelity? FidelityFor(string value) => value.ToLowerInvariant() switch
    {
        "high" => ImageGenerationToolInputFidelity.High,
        "low" => ImageGenerationToolInputFidelity.Low,
        _ => null
    };

    static string ContentTypeFor(ImageGenToolCallOutputFormat? format) =>
        format?.ToString().ToLowerInvariant() switch
        {
            "jpeg" => "image/jpeg",
            "webp" => "image/webp",
            _ => "image/png"
        };

}
