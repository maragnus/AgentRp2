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
    IReadOnlyList<ImageModelOption> GetEnabledImageModels(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState? selections = null);
    Task<GeneratedImageResult> GenerateAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateImageRequest request, CancellationToken cancellationToken = default);
}

public sealed class ImageGenerationService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IModelGenerationClient generationClient,
    IModelCapabilityCatalog capabilityCatalog) : IImageGenerationService
{
    const int MaxImageBytes = 10 * 1024 * 1024;
    static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    public IReadOnlyList<ImageModelOption> GetEnabledImageModels(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState? selections = null)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);

        var options = providers
            .Where(provider => provider.Enabled)
            .SelectMany(provider => provider.Models
                .Where(AiProviderModelSelectionRules.IsSelectedForImage)
                .Select(model => new ImageModelOption(
                    BuildModelKey(provider.Id, model.Id),
                    $"{DisplayName(model)} ({provider.Name})",
                    provider.Id,
                    provider.Name,
                    provider.Type,
                    model.Id)))
            .ToList();

        var active = TextModelTuningCatalog.TryResolveActiveModel(providers, AiModelRole.Image, selections);
        if (active is null)
            return options;

        var activeKey = BuildModelKey(active.Provider.Id, active.Model.Id);
        return options
            .OrderBy(option => option.Key == activeKey ? 0 : 1)
            .ToList();
    }

    public async Task<GeneratedImageResult> GenerateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(document, providers, request.ModelKey);
        var finalPrompt = BuildPrompt(document, request);
        if (string.IsNullOrWhiteSpace(finalPrompt))
            throw new InvalidOperationException("Generating an image failed because the prompt was empty.");

        var size = NormalizeSize(request.Size);
        var quality = NormalizeQuality(request.Quality);
        var referenceImages = await LoadReferenceImagesAsync(document.Chat.Id, request.ReferenceImageIds, cancellationToken);
        var generated = await GenerateResponsesImageAsync(model.Provider, model.Model, finalPrompt, size, quality, request.ReferenceFidelity, referenceImages, cancellationToken);

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

    async Task<GeneratedImage> GenerateResponsesImageAsync(
        AiProvider provider,
        AiProviderModel model,
        string prompt,
        string size,
        string quality,
        string referenceFidelity,
        IReadOnlyList<ResponseImageInput> referenceImages,
        CancellationToken cancellationToken)
    {
        var capabilities = capabilityCatalog.Resolve(provider, model);
        byte[]? lastBytes = null;
        var contentType = "image/png";
        await foreach (var update in generationClient.GenerateStreamingImageAsync(new(
            provider,
            model,
            capabilities,
            prompt,
            size,
            quality,
            referenceFidelity,
            referenceImages,
            $"Generating an image with '{DisplayName(model)}'"), cancellationToken))
        {
            if (update.ImageBytes is { Length: > 0 })
            {
                lastBytes = update.ImageBytes;
                contentType = update.ContentType;
            }
        }

        if (lastBytes is null)
            throw new InvalidOperationException($"{provider.Name} did not return image bytes through Responses image output.");

        return new(lastBytes, contentType, "responses-image.png");
    }

    (AiProvider Provider, AiProviderModel Model) ResolveModel(RpChatDocument document, IReadOnlyList<AiProvider> providers, string modelKey)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);

        if (string.IsNullOrWhiteSpace(modelKey))
        {
            var active = TextModelTuningCatalog.TryResolveActiveModel(providers, AiModelRole.Image, document.ActiveModelSelections);
            if (active is not null)
                return (active.Provider, active.Model);
        }

        foreach (var provider in providers.Where(provider => provider.Enabled))
        {
            foreach (var model in provider.Models.Where(AiProviderModelSelectionRules.IsSelectedForImage))
            {
                if (string.Equals(BuildModelKey(provider.Id, model.Id), modelKey, StringComparison.Ordinal))
                    return (provider, model);
            }
        }

        throw new InvalidOperationException("Generating an image failed because the selected image model is not enabled.");
    }

    async Task<IReadOnlyList<ResponseImageInput>> LoadReferenceImagesAsync(string chatId, IReadOnlyCollection<string> imageIds, CancellationToken cancellationToken)
    {
        if (imageIds.Count == 0)
            return [];

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ImageAssets
            .AsNoTracking()
            .Where(image => image.ChatId == chatId && imageIds.Contains(image.Id))
            .Select(image => new ResponseImageInput(image.Bytes, image.ContentType))
            .ToListAsync(cancellationToken);
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

    static string DisplayName(AiProviderModel model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName;

    public static string BuildImageUrl(string imageId) => $"/story-images/{Uri.EscapeDataString(imageId)}";

    sealed record GeneratedImage(byte[] Bytes, string ContentType, string FileName);
}
