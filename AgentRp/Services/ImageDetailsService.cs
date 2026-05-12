using System.Text.Json;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;
using AgentRp.Session;
using AgentRp.UserSystem;
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
    string Rationale,
    IReadOnlyList<ImageDetailsReferenceView> References,
    bool HasGenerationMetadata);

public sealed record ImageDetailsReferenceView(string Kind, string Name, string EntityType, string ImageUrl);

public interface IImageDetailsService
{
    Task<ImageDetailsView> GetAsync(CurrentAppUser user, string imageId, CancellationToken cancellationToken = default);
}

public sealed class ImageDetailsService(IDbContextFactory<RpDbContext> dbContextFactory) : IImageDetailsService
{
    public async Task<ImageDetailsView> GetAsync(CurrentAppUser user, string imageId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.ImageAssets.AsNoTracking()
            .Where(image => image.UserId == user.Id && image.Id == imageId)
            .OrderBy(image => image.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return FallbackView(null, imageId);

        var galleryImage = StoryEntityPersistenceMapper.ToModel(row);

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
            metadata?.Rationale ?? "",
            BuildReferenceViews(metadata),
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

    static IReadOnlyList<ImageDetailsReferenceView> BuildReferenceViews(ImageAssetGenerationMetadata? metadata) =>
        metadata?.References
            .Select(reference => new ImageDetailsReferenceView(
                reference.Kind,
                reference.Name,
                reference.EntityType,
                FirstNonEmpty(reference.ImageUrl, reference.Kind == "image" ? ImageGenerationService.BuildImageUrl(reference.Id) : "")))
            .ToList()
        ?? [];

    static string FormatDimensions(int? width, int? height) =>
        width is > 0 && height is > 0 ? $"{width} x {height}" : "";

    static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
