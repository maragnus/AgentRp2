using System.Text.Json;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public sealed record ImageDetailsView(
    string Id,
    string Title,
    string Url,
    string Entity,
    string EntityType,
    string CreatedLabel,
    string Dimensions,
    string UserPrompt,
    string FinalPrompt,
    string RevisedPrompt,
    string ProviderName,
    string ModelId,
    string Size,
    string Quality,
    string ReferenceDetail,
    string ArtStyle,
    IReadOnlyList<ImageDetailsReferenceView> References,
    bool HasGenerationMetadata);

public sealed record ImageDetailsReferenceView(string Kind, string Name, string EntityType, string ImageUrl);

public interface IImageDetailsService
{
    Task<ImageDetailsView> GetAsync(RpChatDocument document, string imageId, CancellationToken cancellationToken = default);
}

public sealed class ImageDetailsService(IDbContextFactory<RpDbContext> dbContextFactory) : IImageDetailsService
{
    public async Task<ImageDetailsView> GetAsync(RpChatDocument document, string imageId, CancellationToken cancellationToken = default)
    {
        var galleryImage = document.Images.FirstOrDefault(image => image.Id == imageId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.ImageAssets
            .AsNoTracking()
            .Where(image => image.ChatId == document.Chat.Id && image.Id == imageId)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return FallbackView(galleryImage, imageId);

        var metadata = DeserializeMetadata(row.GenerationMetadataJson);
        return new(
            row.Id,
            FirstNonEmpty(row.Title, galleryImage?.Name, "Image"),
            ImageGenerationService.BuildImageUrl(row.Id),
            galleryImage?.Entity ?? "",
            galleryImage?.EntityType ?? "",
            RelativeDateFormatter.FormatDate(row.CreatedUtc),
            FormatDimensions(row.Width, row.Height),
            row.UserPrompt,
            row.FinalPrompt,
            metadata?.RevisedPrompt ?? "",
            row.ProviderName,
            row.ProviderModelId,
            metadata?.Size ?? "",
            metadata?.Quality ?? "",
            metadata?.ReferenceDetail ?? "",
            metadata?.ArtStyleLabel ?? "",
            BuildReferenceViews(document, metadata),
            metadata is not null);
    }

    static ImageDetailsView FallbackView(GalleryImage? image, string imageId) => new(
        imageId,
        FirstNonEmpty(image?.Name, "Image"),
        image?.Url ?? "",
        image?.Entity ?? "",
        image?.EntityType ?? "",
        image?.Date ?? "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        [],
        false);

    static ImageAssetGenerationMetadata? DeserializeMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ImageAssetGenerationMetadata>(json, AppJsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static IReadOnlyList<ImageDetailsReferenceView> BuildReferenceViews(RpChatDocument document, ImageAssetGenerationMetadata? metadata) =>
        metadata?.References
            .Select(reference => new ImageDetailsReferenceView(
                reference.Kind,
                reference.Name,
                reference.EntityType,
                FirstNonEmpty(reference.ImageUrl, ResolveReferenceImageUrl(document, reference))))
            .ToList()
        ?? [];

    static string ResolveReferenceImageUrl(RpChatDocument document, ImageAssetReferenceMetadata reference)
    {
        if (reference.Kind == "image")
            return document.Images.FirstOrDefault(image => image.Id == reference.Id)?.Url ?? "";

        var imageId = reference.EntityType switch
        {
            "character" => document.Characters.FirstOrDefault(character => character.Id == reference.Id)?.ImageId,
            "location" => document.Locations.FirstOrDefault(location => location.Id == reference.Id)?.ImageId,
            "item" => document.Items.FirstOrDefault(item => item.Id == reference.Id)?.ImageId,
            _ => ""
        };
        return string.IsNullOrWhiteSpace(imageId)
            ? ""
            : document.Images.FirstOrDefault(image => image.Id == imageId)?.Url ?? ImageGenerationService.BuildImageUrl(imageId);
    }

    static string FormatDimensions(int? width, int? height) =>
        width is > 0 && height is > 0 ? $"{width} x {height}" : "";

    static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
