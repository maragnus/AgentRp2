using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public sealed record ImageModelOption(string Key, string Label, string ProviderId, string ProviderName, string ProviderType, string ModelId);

public sealed record GenerateImageRequest(
    string ModelKey,
    string Prompt,
    string Size,
    string Quality,
    string ReferenceFidelity,
    IReadOnlyCollection<string> CharacterIds,
    IReadOnlyCollection<string> ReferenceImageIds,
    string? TargetEntityName,
    string? TargetEntityType);

public sealed record GeneratedImageResult(GalleryImage Image, string FinalPrompt, string ProviderName, string ModelId);

public interface IImageGenerationService
{
    IReadOnlyList<ImageModelOption> GetEnabledImageModels(IReadOnlyList<AiProvider> providers);
    Task<GeneratedImageResult> GenerateAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateImageRequest request, CancellationToken cancellationToken = default);
}

public sealed class ImageGenerationService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IHttpClientFactory httpClientFactory) : IImageGenerationService
{
    const int MaxImageBytes = 10 * 1024 * 1024;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    public IReadOnlyList<ImageModelOption> GetEnabledImageModels(IReadOnlyList<AiProvider> providers) =>
        providers
            .Where(provider => provider.Enabled)
            .SelectMany(provider => provider.Models
                .Where(model => model.Enabled && model.Image)
                .Select(model => new ImageModelOption(
                    BuildModelKey(provider.Id, model.Id),
                    $"{model.Id} ({provider.Name})",
                    provider.Id,
                    provider.Name,
                    provider.Type,
                    model.Id)))
            .ToList();

    public async Task<GeneratedImageResult> GenerateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(providers, request.ModelKey);
        var finalPrompt = BuildPrompt(document, request);
        if (string.IsNullOrWhiteSpace(finalPrompt))
            throw new InvalidOperationException("Generating an image failed because the prompt was empty.");

        var size = NormalizeSize(request.Size);
        var quality = NormalizeQuality(request.Quality);
        var generated = model.Provider.Type switch
        {
            "openai" => await GenerateOpenAiAsync(model.Provider, model.Model, finalPrompt, size, quality, cancellationToken),
            "grok" => await GenerateGrokAsync(model.Provider, model.Model, finalPrompt, size, cancellationToken),
            _ => throw new InvalidOperationException($"Generating an image failed because '{model.Provider.Name}' does not support image generation yet.")
        };

        ValidateImageBytes(generated.ContentType, generated.Bytes, generated.FileName);
        var dimensions = StoryImageDimensions.TryRead(generated.Bytes, generated.ContentType);
        var imageId = $"img-{Guid.NewGuid():N}";
        var title = BuildTitle(request, finalPrompt);
        var now = DateTime.UtcNow;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ImageAssets.Add(new ImageAssetRow
        {
            Id = imageId,
            ChatId = document.Chat.Id,
            Bytes = generated.Bytes,
            ContentType = generated.ContentType,
            FileName = generated.FileName,
            Title = title,
            Width = dimensions?.Width,
            Height = dimensions?.Height,
            UserPrompt = request.Prompt.Trim(),
            FinalPrompt = finalPrompt,
            ProviderId = model.Provider.Id,
            ProviderName = model.Provider.Name,
            ProviderModelId = model.Model.Id,
            CreatedUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var galleryImage = new GalleryImage
        {
            Id = imageId,
            Name = title,
            Entity = string.IsNullOrWhiteSpace(request.TargetEntityName) ? "Generated" : request.TargetEntityName,
            EntityType = GalleryEntityType(request.TargetEntityType),
            Date = "today",
            Hue = 210,
            Url = BuildImageUrl(imageId)
        };

        return new(galleryImage, finalPrompt, model.Provider.Name, model.Model.Id);
    }

    async Task<GeneratedImage> GenerateOpenAiAsync(AiProvider provider, AiProviderModel model, string prompt, string size, string quality, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey, TimeSpan.FromMinutes(5));
        var body = new JsonObject
        {
            ["model"] = model.Id,
            ["prompt"] = prompt,
            ["size"] = size,
            ["quality"] = quality,
            ["n"] = 1
        };

        if (!model.Id.StartsWith("gpt-image-", StringComparison.OrdinalIgnoreCase)
            && !model.Id.StartsWith("chatgpt-image-", StringComparison.OrdinalIgnoreCase))
        {
            body["response_format"] = "b64_json";
        }

        using var response = await client.PostAsJsonAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "images/generations"), body, JsonOptions, cancellationToken);
        var json = await ReadJsonAsync(response, $"Generating an OpenAI image with '{model.Id}'", cancellationToken);
        var b64 = json["data"]?.AsArray().FirstOrDefault()?["b64_json"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(b64))
            throw new InvalidOperationException("OpenAI did not return image bytes.");

        return new(Convert.FromBase64String(b64), "image/png", "openai-image.png");
    }

    async Task<GeneratedImage> GenerateGrokAsync(AiProvider provider, AiProviderModel model, string prompt, string size, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey, TimeSpan.FromMinutes(5));
        var body = new JsonObject
        {
            ["model"] = model.Id,
            ["prompt"] = prompt,
            ["size"] = size
        };

        using var response = await client.PostAsJsonAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "images/generations"), body, JsonOptions, cancellationToken);
        var json = await ReadJsonAsync(response, $"Generating a Grok image with '{model.Id}'", cancellationToken);
        var data = json["data"]?.AsArray().FirstOrDefault();
        var b64 = data?["b64_json"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(b64))
            return new(Convert.FromBase64String(b64), "image/png", "grok-image.png");

        var url = data?["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Grok did not return an image URL or image bytes.");

        using var imageResponse = await client.GetAsync(url, cancellationToken);
        if (!imageResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Downloading the generated Grok image failed because the image endpoint returned {(int)imageResponse.StatusCode} ({imageResponse.StatusCode}).");

        var bytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = imageResponse.Content.Headers.ContentType?.MediaType ?? "image/png";
        return new(bytes, contentType, "grok-image.png");
    }

    static (AiProvider Provider, AiProviderModel Model) ResolveModel(IReadOnlyList<AiProvider> providers, string modelKey)
    {
        foreach (var provider in providers.Where(provider => provider.Enabled))
        {
            foreach (var model in provider.Models.Where(model => model.Enabled && model.Image))
            {
                if (string.Equals(BuildModelKey(provider.Id, model.Id), modelKey, StringComparison.Ordinal))
                    return (provider, model);
            }
        }

        throw new InvalidOperationException("Generating an image failed because the selected image model is not enabled.");
    }

    static string BuildPrompt(RpChatDocument document, GenerateImageRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            parts.Add(request.Prompt.Trim());

        if (!string.IsNullOrWhiteSpace(request.TargetEntityName))
            parts.Add($"Target subject: {request.TargetEntityName.Trim()}.");

        var selectedCharacters = document.Characters
            .Where(character => request.CharacterIds.Contains(character.Id))
            .ToList();
        if (selectedCharacters.Count > 0)
        {
            parts.Add("Character context:");
            foreach (var character in selectedCharacters)
            {
                var details = string.Join(" ", new[] { character.Summary, character.Appearance, character.Personality, character.Notes }.Where(value => !string.IsNullOrWhiteSpace(value)));
                parts.Add($"- {character.Name}: {details}".Trim());
            }
        }

        var activeLocation = document.Locations.FirstOrDefault(location => location.IsActive);
        if (activeLocation is not null)
            parts.Add($"Location: {activeLocation.Name}. {activeLocation.Summary} {activeLocation.Description}".Trim());

        return string.Join("\n", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    static string BuildTitle(GenerateImageRequest request, string finalPrompt)
    {
        var source = !string.IsNullOrWhiteSpace(request.TargetEntityName)
            ? request.TargetEntityName
            : !string.IsNullOrWhiteSpace(request.Prompt)
                ? request.Prompt
                : finalPrompt;
        source = source.Trim().Replace('\n', ' ');
        return source.Length <= 48 ? source : source[..48].TrimEnd();
    }

    static void ValidateImageBytes(string contentType, byte[] bytes, string displayName)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidOperationException($"Adding image '{displayName}' failed because only PNG, JPEG, and WebP images are supported.");
        if (bytes.Length == 0)
            throw new InvalidOperationException($"Adding image '{displayName}' failed because the file was empty.");
        if (bytes.Length > MaxImageBytes)
            throw new InvalidOperationException($"Adding image '{displayName}' failed because images must be 10 MB or smaller.");
    }

    HttpClient CreateBearerClient(string apiKey, TimeSpan timeout)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = timeout;
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken) ?? new JsonObject();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = UserFacingErrorMessageBuilder.BuildExternalHttpFailure(operation, response.StatusCode, responseBody);
        throw new ExternalServiceFailureException(message, response.StatusCode, responseBody);
    }

    static string NormalizeEndpoint(AiProvider provider)
    {
        var endpoint = string.IsNullOrWhiteSpace(provider.Endpoint) ? DefaultEndpoint(provider.Type) : provider.Endpoint.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint was empty.");
        return endpoint.EndsWith('/') ? endpoint : $"{endpoint}/";
    }

    static string DefaultEndpoint(string providerType) => providerType switch
    {
        "openai" => "https://api.openai.com/v1/",
        "grok" => "https://api.x.ai/v1/",
        _ => ""
    };

    static string NormalizeSize(string value) => value switch
    {
        "Portrait" => "1024x1536",
        "Landscape" => "1536x1024",
        "Square" => "1024x1024",
        _ when value.Contains('x', StringComparison.Ordinal) => value,
        _ => "1024x1024"
    };

    static string NormalizeQuality(string value) => value.ToLowerInvariant() switch
    {
        "high" => "high",
        "medium" => "medium",
        "low" => "low",
        _ => "auto"
    };

    static string GalleryEntityType(string? targetEntityType) => targetEntityType switch
    {
        "characters" => "character",
        "locations" => "location",
        "items" => "item",
        _ => "scene"
    };

    public static string BuildModelKey(string providerId, string modelId) => $"{providerId}::{modelId}";

    public static string BuildImageUrl(string imageId) => $"/story-images/{Uri.EscapeDataString(imageId)}";

    sealed record GeneratedImage(byte[] Bytes, string ContentType, string FileName);
}
