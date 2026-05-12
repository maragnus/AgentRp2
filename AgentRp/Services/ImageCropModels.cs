using AgentRp.Models;

namespace AgentRp.Services;

public sealed record ImageAvatarCropView(int FocusXPercent, int FocusYPercent, int ZoomPercent)
{
    public static ImageAvatarCropView Default { get; } = new(50, 50, 100);

    public static ImageAvatarCropView From(GalleryImage image) =>
        Normalize(image.AvatarFocusXPercent, image.AvatarFocusYPercent, image.AvatarZoomPercent);

    public static ImageAvatarCropView Normalize(int focusXPercent, int focusYPercent, int zoomPercent) =>
        new(Math.Clamp(focusXPercent, 0, 100), Math.Clamp(focusYPercent, 0, 100), Math.Clamp(zoomPercent, 100, 300));
}

public sealed record ImageCropView(
    string ImageId,
    string Title,
    string ImageUrl,
    ImageAvatarCropView Crop);

public sealed record UpdateImageCropRequest(
    Guid UserId,
    string ImageId,
    int FocusXPercent,
    int FocusYPercent,
    int ZoomPercent);

public sealed record ImageCropSavedResult(string ImageId, ImageAvatarCropView Crop);
