using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public interface IImageCropService
{
    Task<ImageCropView> GetAsync(RpChatDocument document, string imageId, CancellationToken cancellationToken = default);
    Task<ImageAvatarCropView> UpdateAsync(UpdateImageCropRequest request, CancellationToken cancellationToken = default);
}

public sealed class ImageCropService(IDbContextFactory<RpDbContext> dbContextFactory) : IImageCropService
{
    public async Task<ImageCropView> GetAsync(RpChatDocument document, string imageId, CancellationToken cancellationToken = default)
    {
        var galleryImage = document.Images.FirstOrDefault(image => image.Id == imageId);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.ImageAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(image => image.ChatId == document.Chat.Id && image.Id == imageId, cancellationToken);

        if (row is null && galleryImage is null)
            throw new InvalidOperationException("Cropping the image failed because the image could not be found in this chat.");

        var crop = row is null
            ? ImageAvatarCropView.From(galleryImage!)
            : BuildCrop(row, galleryImage);
        return new(
            imageId,
            FirstNonEmpty(row?.Title, galleryImage?.Name, "Image"),
            FirstNonEmpty(galleryImage?.Url, row is null ? "" : ImageGenerationService.BuildImageUrl(row.Id)),
            crop);
    }

    public async Task<ImageAvatarCropView> UpdateAsync(UpdateImageCropRequest request, CancellationToken cancellationToken = default)
    {
        var crop = ImageAvatarCropView.Normalize(request.FocusXPercent, request.FocusYPercent, request.ZoomPercent);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.ImageAssets
            .FirstOrDefaultAsync(image => image.ChatId == request.ChatId && image.Id == request.ImageId, cancellationToken);
        if (row is not null)
        {
            row.AvatarFocusXPercent = crop.FocusXPercent;
            row.AvatarFocusYPercent = crop.FocusYPercent;
            row.AvatarZoomPercent = crop.ZoomPercent;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return crop;
    }

    static ImageAvatarCropView BuildCrop(ImageAssetRow row, GalleryImage? galleryImage)
    {
        var fallback = galleryImage is null ? ImageAvatarCropView.Default : ImageAvatarCropView.From(galleryImage);
        return ImageAvatarCropView.Normalize(
            row.AvatarFocusXPercent ?? fallback.FocusXPercent,
            row.AvatarFocusYPercent ?? fallback.FocusYPercent,
            row.AvatarZoomPercent ?? fallback.ZoomPercent);
    }

    static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}
