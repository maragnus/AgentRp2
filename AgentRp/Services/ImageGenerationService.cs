using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public sealed record ImageModelOption(string Key, string Label, string ProviderId, string ProviderName, string ProviderType, string ModelId);

public sealed record ImageArtStyleOption(string Key, string Label, string PromptInstruction);

public sealed record StoryImageEntitySelection(string EntityType, string EntityId);

public sealed record ImageGenerationSettingsDocument(string ModelKey, string Size, string Quality, string ReferenceDetail, string ArtStyleKey)
{
    public static ImageGenerationSettingsDocument Default { get; } = new("", StoryImagePromptBuilder.SquareSize, "auto", "low", "none");
}

public sealed record GenerateImageRequest(
    string ModelKey,
    string Prompt,
    string ArtStyleKey,
    string Size,
    string Quality,
    string ReferenceDetail,
    IReadOnlyCollection<StoryImageEntitySelection> Entities,
    IReadOnlyCollection<string> ReferenceImageIds,
    string? TargetEntityName,
    string? TargetEntityType,
    string? TargetEntityId = null);

public sealed record GeneratedImageResult(GalleryImage Image, string FinalPrompt, string ProviderName, string ModelId, string RevisedPrompt = "", string Rationale = "");

public sealed record ImageGenerationStreamingUpdate(string? PreviewImageDataUrl = null, GeneratedImageResult? Result = null, bool Completed = false);

public interface IImageGenerationService
{
    IReadOnlyList<ImageArtStyleOption> GetArtStyleOptions();
    IReadOnlyList<ImageModelOption> GetEnabledImageModels(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState? selections = null);
    Task<ImageGenerationSettingsDocument> GetSettingsAsync(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState? selections = null, CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(ImageGenerationSettingsDocument settings, CancellationToken cancellationToken = default);
    Task<GeneratedImageResult> GenerateAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateImageRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ImageGenerationStreamingUpdate> GenerateStreamingAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateImageRequest request, CancellationToken cancellationToken = default);
}

public sealed class ImageGenerationService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IModelGenerationClient generationClient,
    IModelCapabilityCatalog capabilityCatalog,
    IAppSettingsService? appSettingsService = null) : IImageGenerationService
{
    const int MaxImageBytes = 10 * 1024 * 1024;
    const string ImageSettingsKey = "image-generation-defaults";
    static readonly IReadOnlyList<ImageArtStyleOption> ArtStyleOptions =
    [
        new("none", "None", ""),
        new("cinematic-fantasy", "Cinematic Fantasy", "cinematic fantasy realism with dramatic lighting and production-design detail"),
        new("dark-fantasy", "Dark Fantasy", "dark fantasy concept art with moody contrast, grounded materials, and ominous atmosphere"),
        new("cozy-storybook", "Cozy Storybook", "cozy illustrated storybook art with warm light, readable shapes, and inviting texture"),
        new("anime", "Anime", "polished anime key art with expressive characters, clean linework, and vibrant color"),
        new("graphic-novel", "Graphic Novel", "graphic novel art with confident ink lines, stylized shadows, and panel-ready composition"),
        new("oil-painting", "Oil Painting", "traditional oil painting with visible brushwork, rich color, and layered depth"),
        new("watercolor", "Watercolor", "watercolor illustration with soft pigment blooms, light texture, and airy edges"),
        new("concept-art", "Concept Art", "painterly entertainment concept art with clear silhouette, practical detail, and cinematic composition"),
        new("photoreal-portrait", "Photoreal Portrait", "photoreal character portrait with natural skin, lens-realistic lighting, and believable costume detail"),
        new("isometric-game", "Isometric Game Art", "isometric game art with crisp readable forms, controlled perspective, and attractive asset detail"),
        new("vintage-pulp", "Vintage Pulp Cover", "vintage pulp cover illustration with bold composition, period color, and dramatic adventure energy")
    ];

    static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    public IReadOnlyList<ImageArtStyleOption> GetArtStyleOptions() => ArtStyleOptions;

    public async Task<ImageGenerationSettingsDocument> GetSettingsAsync(
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState? selections = null,
        CancellationToken cancellationToken = default)
    {
        var saved = appSettingsService is null
            ? ImageGenerationSettingsDocument.Default
            : await appSettingsService.GetAsync(ImageSettingsKey, ImageGenerationSettingsDocument.Default, cancellationToken);
        var models = GetEnabledImageModels(providers, selections);
        var modelKey = models.Any(model => model.Key == saved.ModelKey)
            ? saved.ModelKey
            : models.FirstOrDefault()?.Key ?? "";

        return new(
            modelKey,
            NormalizeSize(saved.Size),
            NormalizeQuality(saved.Quality),
            NormalizeReferenceDetail(saved.ReferenceDetail),
            ResolveArtStyle(saved.ArtStyleKey)?.Key ?? "none");
    }

    public async Task SaveSettingsAsync(ImageGenerationSettingsDocument settings, CancellationToken cancellationToken = default)
    {
        if (appSettingsService is null)
            return;

        await appSettingsService.SaveAsync(
            ImageSettingsKey,
            new ImageGenerationSettingsDocument(
                settings.ModelKey.Trim(),
                NormalizeSize(settings.Size),
                NormalizeQuality(settings.Quality),
                NormalizeReferenceDetail(settings.ReferenceDetail),
                ResolveArtStyle(settings.ArtStyleKey)?.Key ?? "none"),
            cancellationToken);
    }

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
        CancellationToken cancellationToken = default) =>
        await GenerateAsync(document, providers, ActiveModelSelectionsState.CreateDefault(), request, cancellationToken);

    public async Task<GeneratedImageResult> GenerateAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateImageRequest request,
        CancellationToken cancellationToken = default)
    {
        GeneratedImageResult? result = null;
        await foreach (var update in GenerateStreamingAsync(document, providers, modelSelections, request, cancellationToken))
        {
            if (update.Result is not null)
                result = update.Result;
        }

        return result ?? throw new InvalidOperationException("Generating an image failed because no completed image was returned.");
    }

    public async IAsyncEnumerable<ImageGenerationStreamingUpdate> GenerateStreamingAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateImageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in GenerateStreamingAsync(document, providers, ActiveModelSelectionsState.CreateDefault(), request, cancellationToken))
            yield return update;
    }

    public async IAsyncEnumerable<ImageGenerationStreamingUpdate> GenerateStreamingAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateImageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = ResolveModels(document, providers, modelSelections, request.ModelKey, request.ReferenceImageIds.Count > 0);
        var prompt = await ComposePromptAsync(document, providers, modelSelections, request, cancellationToken);
        if (string.IsNullOrWhiteSpace(prompt.FinalPrompt))
            throw new InvalidOperationException("Generating an image failed because the prompt was empty.");

        var size = NormalizeSize(request.Size);
        var quality = NormalizeQuality(request.Quality);
        var referenceDetail = NormalizeReferenceDetail(request.ReferenceDetail);
        var referenceImages = await LoadReferenceImagesAsync(document.Chat.Id, request.ReferenceImageIds, cancellationToken);
        GeneratedImage? finalImage = null;
        ResponseImageStreamingUpdate? finalUpdate = null;
        await foreach (var update in generationClient.GenerateStreamingImageAsync(new(
            model.HostProvider,
            model.HostModel,
            model.HostCapabilities,
            model.ImageModel,
            model.ImageCapabilities,
            prompt.FinalPrompt,
            size,
            quality,
            referenceDetail,
            referenceImages,
            $"Generating an image with '{DisplayName(model.ImageModel)}'"), cancellationToken))
        {
            if (update.Completed)
            {
                if (update.ImageBytes is { Length: > 0 })
                {
                    finalImage = new(update.ImageBytes, update.ContentType, "responses-image.png");
                    finalUpdate = update;
                }

                continue;
            }

            if (update.ImageBytes is { Length: > 0 })
                yield return new(BuildImageDataUrl(update.ContentType, update.ImageBytes));
        }

        if (finalImage is null)
            throw new InvalidOperationException($"{model.ImageProvider.Name} did not return image bytes through Responses image output.");

        var result = await SaveGeneratedImageAsync(document, model, request, prompt, size, quality, referenceDetail, finalImage, finalUpdate, cancellationToken);
        yield return new(Result: result, Completed: true);
    }

    async Task<GeneratedImageResult> SaveGeneratedImageAsync(
        RpChatDocument document,
        ResponseImageModelSelection model,
        GenerateImageRequest request,
        ComposedImagePrompt prompt,
        string size,
        string quality,
        string referenceDetail,
        GeneratedImage generated,
        ResponseImageStreamingUpdate? finalUpdate,
        CancellationToken cancellationToken)
    {
        ValidateImageBytes(generated.ContentType, generated.Bytes, generated.FileName);
        var revisedPrompt = finalUpdate?.RevisedPrompt ?? "";
        var dimensions = StoryImageDimensions.TryRead(generated.Bytes, generated.ContentType);
        var imageId = $"img-{Guid.NewGuid():N}";
        var title = BuildTitle(request, prompt.FinalPrompt);
        var now = DateTime.UtcNow;
        var metadata = BuildGenerationMetadata(document, request, size, quality, referenceDetail, prompt.Rationale, revisedPrompt, finalUpdate);

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
            FinalPrompt = prompt.FinalPrompt,
            GenerationMetadataJson = JsonSerializer.Serialize(metadata, AppJsonSerializerOptions.Web),
            ProviderId = model.ImageProvider.Id,
            ProviderName = model.ImageProvider.Name,
            ProviderModelId = model.ImageModel.Id,
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

        return new(galleryImage, prompt.FinalPrompt, model.ImageProvider.Name, model.ImageModel.Id, revisedPrompt, prompt.Rationale);
    }

    ResponseImageModelSelection ResolveModels(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        string modelKey,
        bool needsImageInput)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);

        var imageModel = ResolveImageModel(providers, modelSelections, modelKey);
        var hostModel = ResolveHostModel(providers, modelSelections, imageModel.Provider, imageModel.Model, needsImageInput);
        return new(
            imageModel.Provider,
            imageModel.Model,
            imageModel.Model.Capabilities,
            hostModel.Provider,
            hostModel.Model,
            hostModel.Model.Capabilities);
    }

    (AiProvider Provider, AiProviderModel Model) ResolveImageModel(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey))
        {
            var active = TextModelTuningCatalog.TryResolveActiveModel(providers, AiModelRole.Image, modelSelections);
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

    (AiProvider Provider, AiProviderModel Model) ResolveHostModel(
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        AiProvider imageProvider,
        AiProviderModel imageModel,
        bool needsImageInput)
    {
        if (CanHostResponsesImageGeneration(imageModel.Capabilities, needsImageInput))
            return (imageProvider, imageModel);

        var activeTextModel = TextModelTuningCatalog.TryResolveActiveTextModel(providers, modelSelections);
        if (activeTextModel is not null
            && string.Equals(activeTextModel.Provider.Id, imageProvider.Id, StringComparison.Ordinal)
            && CanHostResponsesImageGeneration(activeTextModel.Model.Capabilities, needsImageInput))
            return (activeTextModel.Provider, activeTextModel.Model);

        foreach (var model in imageProvider.Models.Where(AiProviderModelSelectionRules.IsSelectedForChat))
        {
            if (CanHostResponsesImageGeneration(model.Capabilities, needsImageInput))
                return (imageProvider, model);
        }

        foreach (var model in imageProvider.Models.Where(model => model.Enabled))
        {
            if (CanHostResponsesImageGeneration(model.Capabilities, needsImageInput))
                return (imageProvider, model);
        }

        var requirement = needsImageInput ? "vision-capable Responses chat model" : "Responses chat model";
        throw new InvalidOperationException($"Generating an image with '{DisplayName(imageModel)}' failed because no {requirement} is enabled for {imageProvider.Name}.");
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

    static ImageAssetGenerationMetadata BuildGenerationMetadata(
        RpChatDocument document,
        GenerateImageRequest request,
        string size,
        string quality,
        string referenceDetail,
        string rationale,
        string revisedPrompt,
        ResponseImageStreamingUpdate? finalUpdate)
    {
        var artStyle = ResolveArtStyle(request.ArtStyleKey);
        return new()
        {
            Size = size,
            Quality = quality,
            ReferenceDetail = referenceDetail,
            ArtStyleKey = request.ArtStyleKey,
            ArtStyleLabel = artStyle?.Label ?? "",
            Rationale = rationale,
            RevisedPrompt = revisedPrompt,
            ResponseId = finalUpdate?.ResponseId ?? "",
            InputTokens = finalUpdate?.InputTokens ?? 0,
            OutputTokens = finalUpdate?.OutputTokens ?? 0,
            References = BuildExplicitReferences(document, request)
        };
    }

    static List<ImageAssetReferenceMetadata> BuildExplicitReferences(RpChatDocument document, GenerateImageRequest request)
    {
        var references = new List<ImageAssetReferenceMetadata>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(ImageAssetReferenceMetadata reference)
        {
            var key = $"{reference.Kind}:{reference.EntityType}:{reference.Id}:{reference.Name}";
            if (seen.Add(key))
                references.Add(reference);
        }

        if (!string.IsNullOrWhiteSpace(request.TargetEntityName))
            Add(new()
            {
                Kind = "entity",
                Id = request.TargetEntityId ?? "",
                Name = request.TargetEntityName.Trim(),
                EntityType = GalleryEntityType(request.TargetEntityType)
            });

        foreach (var entity in NormalizeEntitySelections(request.Entities))
        {
            var view = ResolveEntityView(document, entity);
            if (view is null)
                continue;

            Add(new()
            {
                Kind = "entity",
                Id = view.Id,
                Name = view.Name,
                EntityType = view.EntityType,
                ImageUrl = ImageUrlFor(document, view.ImageId)
            });
        }

        foreach (var image in document.Images.Where(image => request.ReferenceImageIds.Contains(image.Id)))
        {
            Add(new()
            {
                Kind = "image",
                Id = image.Id,
                Name = image.Name,
                EntityType = image.EntityType,
                ImageUrl = image.Url
            });
        }

        return references;
    }

    static string ImageUrlFor(RpChatDocument document, string imageId) =>
        string.IsNullOrWhiteSpace(imageId)
            ? ""
            : document.Images.FirstOrDefault(image => image.Id == imageId)?.Url ?? BuildImageUrl(imageId);

    async Task<ComposedImagePrompt> ComposePromptAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateImageRequest request,
        CancellationToken cancellationToken)
    {
        var fallback = BuildPrompt(document, request);
        var selection = TryResolveStructuredTextModel(providers, modelSelections);
        if (selection is null)
            return new(fallback, "Composed the prompt from the user request and selected story context.");

        try
        {
            var completion = await generationClient.GenerateStructuredAsync<ImagePromptResponse>(
                new(
                    selection.Provider,
                    selection.Model,
                    selection.Model.Capabilities,
                    new(),
                    "You turn roleplaying story context into concise, vivid image generation prompts. Return typed structured output only.",
                    BuildPromptComposerUserPrompt(document, request),
                    "Composing image generation prompt"),
                cancellationToken);
            var finalPrompt = string.IsNullOrWhiteSpace(completion.Value.FinalPrompt)
                ? fallback
                : completion.Value.FinalPrompt.Trim();
            var rationale = string.IsNullOrWhiteSpace(completion.Value.Rationale)
                ? "Composed the prompt from the user request and selected story context."
                : completion.Value.Rationale.Trim();
            return new(finalPrompt, rationale);
        }
        catch (Exception) when (!string.IsNullOrWhiteSpace(fallback))
        {
            return new(fallback, "Used deterministic prompt composition because typed prompt generation was unavailable.");
        }
    }

    static string BuildPrompt(RpChatDocument document, GenerateImageRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Prompt))
            parts.Add(request.Prompt.Trim());

        var artStyle = ResolveArtStyle(request.ArtStyleKey);
        if (!string.IsNullOrWhiteSpace(artStyle?.PromptInstruction))
            parts.Add($"Art style: {artStyle.PromptInstruction}.");

        if (!string.IsNullOrWhiteSpace(request.TargetEntityName))
            parts.Add($"Target subject: {request.TargetEntityName.Trim()}.");

        var selectedEntities = NormalizeEntitySelections(request.Entities)
            .Select(entity => ResolveEntityView(document, entity))
            .OfType<EntityContextView>()
            .ToList();
        if (selectedEntities.Count > 0)
        {
            parts.Add("Story entity context:");
            foreach (var entity in selectedEntities)
            {
                var details = string.Join(" ", entity.Details.Where(value => !string.IsNullOrWhiteSpace(value)));
                parts.Add($"- {entity.Label}: {details}".Trim());
            }
        }

        var activeLocation = document.Locations.FirstOrDefault(location => location.IsActive);
        if (activeLocation is not null)
            parts.Add($"Location: {activeLocation.Name}. {activeLocation.Summary} {activeLocation.Description}".Trim());

        return string.Join("\n", parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    static string BuildPromptComposerUserPrompt(RpChatDocument document, GenerateImageRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("User request:");
        builder.AppendLine(string.IsNullOrWhiteSpace(request.Prompt) ? "(none)" : request.Prompt.Trim());
        builder.AppendLine();

        var artStyle = ResolveArtStyle(request.ArtStyleKey);
        if (!string.IsNullOrWhiteSpace(artStyle?.PromptInstruction))
        {
            builder.AppendLine("Art style:");
            builder.AppendLine(artStyle.PromptInstruction);
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(request.TargetEntityName))
        {
            builder.AppendLine("Target entity:");
            builder.AppendLine($"{GalleryEntityType(request.TargetEntityType)}: {request.TargetEntityName.Trim()}");
            builder.AppendLine();
        }

        builder.AppendLine("Selected story entities:");
        var selected = NormalizeEntitySelections(request.Entities)
            .Select(entity => ResolveEntityView(document, entity))
            .OfType<EntityContextView>()
            .ToList();
        if (selected.Count == 0)
        {
            builder.AppendLine("No story entities selected.");
        }
        else
        {
            foreach (var entity in selected)
            {
                builder.AppendLine(entity.Label);
                foreach (var detail in entity.Details.Where(value => !string.IsNullOrWhiteSpace(value)))
                    builder.AppendLine(detail.Trim());
                builder.AppendLine();
            }
        }

        var activeLocation = document.Locations.FirstOrDefault(location => location.IsActive);
        if (activeLocation is not null)
        {
            builder.AppendLine("Active location:");
            builder.AppendLine($"{activeLocation.Name}. {activeLocation.Summary} {activeLocation.Description}".Trim());
            builder.AppendLine();
        }

        builder.AppendLine("Write a final image prompt that preserves the user's intent and incorporates the selected entity details. Avoid prose explanations inside the final prompt.");
        return builder.ToString().Trim();
    }

    static ImageArtStyleOption? ResolveArtStyle(string key) =>
        ArtStyleOptions.FirstOrDefault(style => string.Equals(style.Key, key, StringComparison.Ordinal));

    static string PronounText(string pronouns) =>
        string.IsNullOrWhiteSpace(pronouns) ? "" : $"Pronouns: {pronouns}.";

    ActiveModelSelection? TryResolveStructuredTextModel(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState? selections)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(providers, selections);
        if (active is not null && active.Model.Capabilities.CanGenerateStructuredText)
            return active;

        foreach (var provider in providers.Where(provider => provider.Enabled))
        {
            foreach (var model in provider.Models.Where(AiProviderModelSelectionRules.IsSelectedForChat))
            {
                if (model.Capabilities.CanGenerateStructuredText)
                    return new ActiveModelSelection(provider, model, model.Capabilities, AiModelRole.Chat);
            }
        }

        return null;
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

    static string NormalizeReferenceDetail(string value) => value.ToLowerInvariant() switch
    {
        "high" => "high",
        "low" => "low",
        _ => "auto"
    };

    static string BuildImageDataUrl(string contentType, byte[] bytes) =>
        $"data:{(string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType)};base64,{Convert.ToBase64String(bytes)}";

    static string GalleryEntityType(string? targetEntityType) => targetEntityType switch
    {
        "characters" => "character",
        "character" => "character",
        "locations" => "location",
        "location" => "location",
        "items" => "item",
        "item" => "item",
        _ => "scene"
    };

    static IReadOnlyList<StoryImageEntitySelection> NormalizeEntitySelections(IReadOnlyCollection<StoryImageEntitySelection> entities)
    {
        var result = new List<StoryImageEntitySelection>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in entities)
        {
            var type = GalleryEntityType(entity.EntityType);
            if (type == "scene" || string.IsNullOrWhiteSpace(entity.EntityId))
                continue;

            var id = entity.EntityId.Trim();
            if (seen.Add($"{type}:{id}"))
                result.Add(new(type, id));
        }

        return result;
    }

    static EntityContextView? ResolveEntityView(RpChatDocument document, StoryImageEntitySelection entity) =>
        entity.EntityType switch
        {
            "character" => document.Characters
                .Where(character => character.Id == entity.EntityId)
                .Select(character => new EntityContextView(
                    character.Id,
                    character.Name,
                    "character",
                    character.ImageId,
                    $"Character: {character.Name}",
                    [
                        PronounText(CharacterProfileRules.FormatPronouns(character.Pronouns)),
                        character.Summary,
                        CharacterAppearanceFormatter.FormatBase(character, document.CharacterTraitLibrary),
                        character.Personality,
                        character.Notes
                    ]))
                .FirstOrDefault(),
            "location" => document.Locations
                .Where(location => location.Id == entity.EntityId)
                .Select(location => new EntityContextView(
                    location.Id,
                    location.Name,
                    "location",
                    location.ImageId,
                    $"Location: {location.Name}",
                    [location.Summary, location.Description, location.Atmosphere, location.Features]))
                .FirstOrDefault(),
            "item" => document.Items
                .Where(item => item.Id == entity.EntityId)
                .Select(item => new EntityContextView(
                    item.Id,
                    item.Name,
                    "item",
                    item.ImageId,
                    $"Item: {item.Name}",
                    [item.Summary, item.Description, item.History, item.Properties]))
                .FirstOrDefault(),
            _ => null
        };

    public static string BuildModelKey(string providerId, string modelId) => $"{providerId}::{modelId}";

    static string DisplayName(AiProviderModel model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName;

    public static string BuildImageUrl(string imageId) => $"/story-images/{Uri.EscapeDataString(imageId)}";

    static bool CanHostResponsesImageGeneration(ModelGenerationCapabilities capabilities, bool needsImageInput) =>
        capabilities.CanGenerateText && capabilities.Tools && (!needsImageInput || capabilities.ImageInput);

    sealed record ResponseImageModelSelection(
        AiProvider ImageProvider,
        AiProviderModel ImageModel,
        ModelGenerationCapabilities ImageCapabilities,
        AiProvider HostProvider,
        AiProviderModel HostModel,
        ModelGenerationCapabilities HostCapabilities);

    sealed record GeneratedImage(byte[] Bytes, string ContentType, string FileName);

    sealed record ComposedImagePrompt(string FinalPrompt, string Rationale);

    sealed record EntityContextView(string Id, string Name, string EntityType, string ImageId, string Label, IReadOnlyList<string> Details);

    sealed record ImagePromptResponse
    {
        [Description("The final image generation prompt.")]
        public string FinalPrompt { get; init; } = "";

        [Description("One short sentence explaining how the entity context was incorporated.")]
        public string Rationale { get; init; } = "";
    }
}
