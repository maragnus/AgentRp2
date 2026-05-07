#pragma warning disable OPENAI001

using System.ClientModel;
using System.Net;
using System.Text;
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
    AiProviderModel HostModel,
    ModelGenerationCapabilities HostCapabilities,
    AiProviderModel ImageModel,
    ModelGenerationCapabilities ImageCapabilities,
    string Prompt,
    string Size,
    string Quality,
    string ReferenceDetail,
    IReadOnlyList<ResponseImageInput> ReferenceImages,
    string OperationName);

public record ModelTextCompletion(string Text, int InputTokens, int OutputTokens, string ResponseId);

public sealed record ModelStructuredCompletion<T>(T Value, string Text, int InputTokens, int OutputTokens, string ResponseId)
    : ModelTextCompletion(Text, InputTokens, OutputTokens, ResponseId);

public sealed record ModelTextStreamingUpdate(string TextDelta = "", int InputTokens = 0, int OutputTokens = 0, string ResponseId = "", bool Completed = false);

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
    string PreviousResponseId,
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
    int InputTokens = 0,
    int OutputTokens = 0);

public sealed class ModelAssistantThreadLostException(
    string providerName,
    string modelId,
    string previousResponseId,
    Exception innerException) : InvalidOperationException(
        $"Story Assistant needs a fresh thread because {providerName} no longer has the saved response '{previousResponseId}' for '{modelId}'. Clear and restart the Story Assistant; your story entities and story are unchanged.",
        innerException);

public interface IModelGenerationClient
{
    Task<ModelStructuredCompletion<T>> GenerateStructuredAsync<T>(ModelGenerationRequest request, CancellationToken cancellationToken = default);
    Task<ModelTextCompletion> GenerateTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default);
    Task<ModelTextCompletion> GenerateStreamingTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ModelTextStreamingUpdate> GenerateStreamingTextUpdatesAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ModelAssistantStreamingUpdate> GenerateAssistantStreamingAsync(ModelAssistantRequest request, CancellationToken cancellationToken = default);
    Task DeleteAssistantResponsesAsync(AiProvider provider, AiProviderModel model, IReadOnlyCollection<string> responseIds, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ResponseImageStreamingUpdate> GenerateStreamingImageAsync(ResponseImageGenerationRequest request, CancellationToken cancellationToken = default);
}

public sealed class OpenAiModelGenerationClient(
    IModelClientFactory clientFactory,
    ILogger<OpenAiModelGenerationClient> logger) : IModelGenerationClient
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
        catch (Exception exception) when (exception is not InvalidOperationException && !cancellationToken.IsCancellationRequested)
        {
            LogFailure(exception, request, "typed structured output");
            throw new InvalidOperationException($"{request.OperationName} failed while requesting typed structured output from {request.Provider.Name} with '{request.Model.Id}': {exception.Message}", exception);
        }
    }

    public async Task<ModelTextCompletion> GenerateTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateText)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not support text input and output.");

        try
        {
            var response = await clientFactory.GetChatClient(request.Provider, request.Model).GetResponseAsync(
                BuildMessages(request),
                BuildChatOptions(request),
                cancellationToken);
            return ToCompletion(response);
        }
        catch (Exception exception) when (exception is not InvalidOperationException && !cancellationToken.IsCancellationRequested)
        {
            LogFailure(exception, request, "text output");
            throw;
        }
    }

    public async Task<ModelTextCompletion> GenerateStreamingTextAsync(ModelGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder();
        var inputTokens = 0;
        var outputTokens = 0;
        var responseId = "";
        await foreach (var update in GenerateStreamingTextUpdatesAsync(request, cancellationToken))
        {
            text.Append(update.TextDelta);
            if (!update.Completed)
                continue;

            inputTokens = update.InputTokens;
            outputTokens = update.OutputTokens;
            responseId = update.ResponseId;
        }

        return new(text.ToString(), inputTokens, outputTokens, responseId);
    }

    public async IAsyncEnumerable<ModelTextStreamingUpdate> GenerateStreamingTextUpdatesAsync(
        ModelGenerationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateText)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not support text input and output.");

        var updates = new List<ChatResponseUpdate>();
        var source = clientFactory.GetChatClient(request.Provider, request.Model).GetStreamingResponseAsync(
            BuildMessages(request),
            BuildChatOptions(request),
            cancellationToken);
        await foreach (var update in LogStreamingFailuresAsync(
            source,
            new(request.OperationName, request.Provider.Name, request.Model.Id, EndpointFor(request.Provider)),
            cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            updates.Add(update);
            if (!string.IsNullOrEmpty(update.Text))
                yield return new(TextDelta: update.Text);
        }

        var completion = ToCompletion(await EnumerateUpdates(updates, cancellationToken).ToChatResponseAsync(cancellationToken));
        yield return new(InputTokens: completion.InputTokens, OutputTokens: completion.OutputTokens, ResponseId: completion.ResponseId, Completed: true);
    }

    public async IAsyncEnumerable<ModelAssistantStreamingUpdate> GenerateAssistantStreamingAsync(
        ModelAssistantRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateText || !request.Capabilities.Tools)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' must support text and tools.");

        var source = clientFactory.GetResponsesClient(request.Provider, request.Model).CreateResponseStreamingAsync(BuildAssistantOptions(request), cancellationToken);
        await foreach (var update in LogStreamingFailuresAsync(
            source,
            new(request.OperationName, request.Provider.Name, request.Model.Id, EndpointFor(request.Provider)),
            cancellationToken,
            exception => ToThreadLostException(exception, request)))
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
                    InputTokens: usage?.InputTokenCount ?? 0,
                    OutputTokens: usage?.OutputTokenCount ?? 0);
            }
            else if (update is StreamingResponseFailedUpdate failed)
            {
                var exception = LoggedStreamFailure(
                    $"{request.OperationName} failed because {request.Provider.Name} returned a failed Responses assistant stream: {failed.Response?.Error?.Message ?? "No failure detail was provided."}",
                    new(request.OperationName, request.Provider.Name, request.Model.Id, EndpointFor(request.Provider)));
                throw ToThreadLostException(exception, request) ?? exception;
            }
            else if (update is StreamingResponseErrorUpdate error)
            {
                var exception = LoggedStreamFailure(
                    $"{request.OperationName} failed because {request.Provider.Name} returned a Responses assistant stream error: {error.Message}",
                    new(request.OperationName, request.Provider.Name, request.Model.Id, EndpointFor(request.Provider)));
                throw ToThreadLostException(exception, request) ?? exception;
            }
        }
    }

    public async Task DeleteAssistantResponsesAsync(
        AiProvider provider,
        AiProviderModel model,
        IReadOnlyCollection<string> responseIds,
        CancellationToken cancellationToken = default)
    {
        var uniqueResponseIds = responseIds
            .Where(responseId => !string.IsNullOrWhiteSpace(responseId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (uniqueResponseIds.Count == 0)
            return;

        var client = clientFactory.GetResponsesClient(provider, model);
        foreach (var responseId in uniqueResponseIds)
        {
            try
            {
                await client.DeleteResponseAsync(responseId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ClientResultException exception) when (exception.Status == (int)HttpStatusCode.NotFound)
            {
                logger.LogInformation(
                    exception,
                    "Stored assistant response was already unavailable during cleanup. Provider: {Provider}; Model: {Model}; ResponseId: {ResponseId}",
                    provider.Name,
                    model.Id,
                    responseId);
            }
            catch (Exception exception)
            {
                ExternalApiFailureLogger.LogModelFailure(
                    logger,
                    exception,
                    new($"Deleting stored assistant response {responseId}", provider.Name, model.Id, EndpointFor(provider)));
            }
        }
    }

    public async IAsyncEnumerable<ResponseImageStreamingUpdate> GenerateStreamingImageAsync(
        ResponseImageGenerationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!request.HostCapabilities.CanGenerateText || !request.HostCapabilities.Tools)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.HostModel.Id}' must support Responses text and tools.");

        if (!request.ImageCapabilities.CanGenerateImage)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.ImageModel.Id}' does not have Responses image output enabled.");

        var source = clientFactory.GetResponsesClient(request.Provider, request.HostModel).CreateResponseStreamingAsync(BuildImageOptions(request), cancellationToken);
        await foreach (var update in LogStreamingFailuresAsync(
            source,
            new(request.OperationName, request.Provider.Name, request.HostModel.Id, EndpointFor(request.Provider)),
            cancellationToken))
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
                throw LoggedStreamFailure(
                    $"{request.OperationName} failed because {request.Provider.Name} returned a failed Responses image stream: {failed.Response?.Error?.Message ?? "No failure detail was provided."}",
                    new(request.OperationName, request.Provider.Name, request.HostModel.Id, EndpointFor(request.Provider)));
            }
            else if (update is StreamingResponseErrorUpdate error)
            {
                throw LoggedStreamFailure(
                    $"{request.OperationName} failed because {request.Provider.Name} returned a Responses image stream error: {error.Message}",
                    new(request.OperationName, request.Provider.Name, request.HostModel.Id, EndpointFor(request.Provider)));
            }
        }
    }

    async IAsyncEnumerable<T> LogStreamingFailuresAsync<T>(
        IAsyncEnumerable<T> source,
        ExternalApiCallLogContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        Func<Exception, Exception?>? mapException = null)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            T current;
            try
            {
                if (!await enumerator.MoveNextAsync())
                    yield break;

                current = enumerator.Current;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ExternalApiFailureLogger.LogModelFailure(logger, exception, context);
                var mapped = mapException?.Invoke(exception);
                if (mapped is not null)
                    throw mapped;

                throw;
            }

            yield return current;
        }
    }

    InvalidOperationException LoggedStreamFailure(string message, ExternalApiCallLogContext context)
    {
        var exception = new InvalidOperationException(message);
        ExternalApiFailureLogger.LogModelFailure(logger, exception, context);
        return exception;
    }

    void LogFailure(Exception exception, ModelGenerationRequest request, string outputKind) =>
        ExternalApiFailureLogger.LogModelFailure(
            logger,
            exception,
            new($"{request.OperationName} requesting {outputKind}", request.Provider.Name, request.Model.Id, EndpointFor(request.Provider)));

    static ModelAssistantThreadLostException? ToThreadLostException(Exception exception, ModelAssistantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PreviousResponseId) || !IsRemoteThreadNotFound(exception))
            return null;

        return new(request.Provider.Name, request.Model.Id, request.PreviousResponseId, exception);
    }

    static bool IsRemoteThreadNotFound(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ClientResultException { Status: (int)HttpStatusCode.NotFound })
                return true;

            if (current is ExternalServiceFailureException { StatusCode: HttpStatusCode.NotFound })
                return true;

            if (current.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static string EndpointFor(AiProvider provider) =>
        string.IsNullOrWhiteSpace(provider.Endpoint)
            ? AiProviderEndpointRules.DefaultEndpoint(provider.Type)
            : provider.Endpoint;

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
            Model = request.HostModel.Id,
            Instructions = "Generate the requested image through the Responses image generation tool.",
            StoredOutputEnabled = false,
            StreamingEnabled = true,
            ToolChoice = ResponseToolChoice.CreateRequiredChoice()
        };
        var imageGenerationModel = string.IsNullOrWhiteSpace(request.ImageCapabilities.ImageGenerationModel)
            ? request.ImageModel.Id
            : request.ImageCapabilities.ImageGenerationModel;
        options.Tools.Add(ResponseTool.CreateImageGenerationTool(
            imageGenerationModel,
            QualityFor(request.Quality),
            SizeFor(request.Size),
            ImageGenerationToolOutputFileFormat.Png,
            null,
            null,
            null,
            InputFidelityFor(request),
            null,
            2,
            ImageGenerationToolAction.Generate));

        if (request.ReferenceImages.Count == 0)
        {
            options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.Prompt));
            return options;
        }

        var parts = new List<ResponseContentPart> { ResponseContentPart.CreateInputTextPart(request.Prompt) };
        foreach (var image in request.ReferenceImages)
            parts.Add(ResponseContentPart.CreateInputImagePart(BinaryData.FromBytes(image.Bytes), ReferenceImageDetailFor(request.ReferenceDetail)));

        options.InputItems.Add(ResponseItem.CreateUserMessageItem(parts));
        return options;
    }

    static CreateResponseOptions BuildAssistantOptions(ModelAssistantRequest request)
    {
        var tuning = TextModelTuningCatalog.Filter(request.Tuning, request.Capabilities);
        var previousResponseId = string.IsNullOrWhiteSpace(request.PreviousResponseId) ? null : request.PreviousResponseId;
        var options = new CreateResponseOptions
        {
            Model = request.Model.Id,
            Instructions = previousResponseId is null ? request.Instructions : null,
            PreviousResponseId = previousResponseId,
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

    static ImageGenerationToolInputFidelity? FidelityFor(string value)
    {
        if (value.Equals("high", StringComparison.OrdinalIgnoreCase))
            return ImageGenerationToolInputFidelity.High;

        if (value.Equals("low", StringComparison.OrdinalIgnoreCase))
            return ImageGenerationToolInputFidelity.Low;

        return null;
    }

    static ImageGenerationToolInputFidelity? InputFidelityFor(ResponseImageGenerationRequest request) =>
        request.ImageCapabilities.ImageInputFidelity ? FidelityFor(request.ReferenceDetail) : null;

    static ResponseImageDetailLevel ReferenceImageDetailFor(string value)
    {
        if (value.Equals("high", StringComparison.OrdinalIgnoreCase))
            return ResponseImageDetailLevel.High;

        if (value.Equals("low", StringComparison.OrdinalIgnoreCase))
            return ResponseImageDetailLevel.Low;

        return ResponseImageDetailLevel.Auto;
    }

    static string ContentTypeFor(ImageGenToolCallOutputFormat? format) =>
        format?.ToString().ToLowerInvariant() switch
        {
            "jpeg" => "image/jpeg",
            "webp" => "image/webp",
            _ => "image/png"
        };

}
