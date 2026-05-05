#pragma warning disable OPENAI001

using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentRp.Models;
using AgentRp.Session;
using OpenAI;
using OpenAI.Responses;

namespace AgentRp.Services;

public sealed record ResponseGenerationRequest(
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

public sealed record ResponseCompletion(string Text, int InputTokens, int OutputTokens, string ResponseId);

public sealed record ResponseCompletion<T>(T Value, string Text, int InputTokens, int OutputTokens, string ResponseId);

public sealed record ResponseStreamingUpdate(string TextDelta, int InputTokens, int OutputTokens, string ResponseId, bool Completed);

public sealed record ResponseImageStreamingUpdate(byte[]? ImageBytes, string ContentType, string? RevisedPrompt, int InputTokens, int OutputTokens, string ResponseId, bool Completed);

public interface IResponseGenerationClient
{
    Task<ResponseCompletion<T>> GetResponseAsync<T>(ResponseGenerationRequest request, CancellationToken cancellationToken = default);
    Task<ResponseCompletion> GetResponseAsync(ResponseGenerationRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ResponseStreamingUpdate> GetStreamingResponseAsync(ResponseGenerationRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ResponseImageStreamingUpdate> GetStreamingImageAsync(ResponseImageGenerationRequest request, CancellationToken cancellationToken = default);
}

public sealed class OpenAiResponsesGenerationClient : IResponseGenerationClient
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResponseCompletion<T>> GetResponseAsync<T>(ResponseGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateStructuredText)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not have structured Responses output enabled.");

        var response = await CreateClient(request.Provider, request.Model).CreateResponseAsync(BuildTextOptions(request, true), cancellationToken);
        var completion = ToCompletion(response.Value, request.Provider.Name);
        var value = DeserializeStructured<T>(completion.Text, request.Provider.Name);
        return new(value, completion.Text, completion.InputTokens, completion.OutputTokens, completion.ResponseId);
    }

    public async Task<ResponseCompletion> GetResponseAsync(ResponseGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateText)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not support text input and output through Responses.");

        var response = await CreateClient(request.Provider, request.Model).CreateResponseAsync(BuildTextOptions(request, false), cancellationToken);
        return ToCompletion(response.Value, request.Provider.Name);
    }

    public async IAsyncEnumerable<ResponseStreamingUpdate> GetStreamingResponseAsync(
        ResponseGenerationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateStreamingText)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not have Responses streaming enabled.");

        await foreach (var update in CreateClient(request.Provider, request.Model).CreateResponseStreamingAsync(BuildTextOptions(request, false), cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (update is StreamingResponseOutputTextDeltaUpdate delta && !string.IsNullOrEmpty(delta.Delta))
            {
                yield return new(delta.Delta, 0, 0, "", false);
            }
            else if (update is StreamingResponseCompletedUpdate completed)
            {
                var usage = completed.Response.Usage;
                yield return new("", usage?.InputTokenCount ?? 0, usage?.OutputTokenCount ?? 0, completed.Response.Id, true);
            }
            else if (update is StreamingResponseFailedUpdate failed)
            {
                throw new InvalidOperationException($"{request.OperationName} failed because {request.Provider.Name} returned a failed Responses stream: {failed.Response?.Error?.Message ?? "No failure detail was provided."}");
            }
            else if (update is StreamingResponseErrorUpdate error)
            {
                throw new InvalidOperationException($"{request.OperationName} failed because {request.Provider.Name} returned a Responses stream error: {error.Message}");
            }
        }
    }

    public async IAsyncEnumerable<ResponseImageStreamingUpdate> GetStreamingImageAsync(
        ResponseImageGenerationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!request.Capabilities.CanGenerateImage)
            throw new InvalidOperationException($"{request.OperationName} failed because '{request.Model.Id}' does not have Responses image output enabled.");

        await foreach (var update in CreateClient(request.Provider, request.Model).CreateResponseStreamingAsync(BuildImageOptions(request), cancellationToken))
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

    static CreateResponseOptions BuildTextOptions(ResponseGenerationRequest request, bool structured)
    {
        var options = new CreateResponseOptions
        {
            Model = request.Model.Id,
            Instructions = request.SystemPrompt,
            StoredOutputEnabled = false,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = structured
                    ? ResponseTextFormat.CreateJsonObjectFormat()
                    : ResponseTextFormat.CreateTextFormat()
            }
        };
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.UserPrompt));
        ApplyTuning(options, TextModelTuningCatalog.Filter(request.Tuning, request.Capabilities));
        return options;
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

    static void ApplyTuning(CreateResponseOptions options, ResponseTuningOptions tuning)
    {
        options.Temperature = tuning.Temperature;
        options.TopP = tuning.TopP;
        options.MaxOutputTokenCount = tuning.MaxOutputTokenCount;
    }

    static ResponsesClient CreateClient(AiProvider provider, AiProviderModel model)
    {
        var endpoint = NormalizeEndpoint(provider, model);
        return new OpenAIClient(
            new ApiKeyCredential(provider.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
            .GetResponsesClient();
    }

    static ResponseCompletion ToCompletion(ResponseResult response, string providerName)
    {
        var text = response.GetOutputText();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"{providerName} did not return any Responses text output.");

        var usage = response.Usage;
        return new(text, usage?.InputTokenCount ?? 0, usage?.OutputTokenCount ?? 0, response.Id);
    }

    static T DeserializeStructured<T>(string content, string providerName)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new InvalidOperationException($"{providerName} returned an empty structured Responses output.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{providerName} returned structured Responses output that could not be read as the requested DTO.", exception);
        }
    }

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

    static string NormalizeEndpoint(AiProvider provider, AiProviderModel model)
    {
        var endpoint = provider.Type == "huggingface" && !string.IsNullOrWhiteSpace(model.Endpoint)
            ? model.Endpoint.Trim()
            : string.IsNullOrWhiteSpace(provider.Endpoint) ? DefaultEndpoint(provider.Type) : provider.Endpoint.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint was empty. Responses/Open Responses providers must use a /v1-compatible base URL.");

        if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint must start with http:// or https://.");

        if (provider.Type == "huggingface")
            return AgentEndpointUrlNormalizer.NormalizeResponsesEndpoint(endpoint);

        return endpoint.EndsWith('/') ? endpoint : $"{endpoint}/";
    }

    static string DefaultEndpoint(string providerType) => providerType switch
    {
        "openai" => "https://api.openai.com/v1/",
        "grok" => "https://api.x.ai/v1/",
        _ => ""
    };
}
