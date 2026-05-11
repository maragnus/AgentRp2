using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Components.Common;

public sealed record CharacterAvatarItem(string Id, string Name, string? ImageUrl, ImageAvatarCropView Crop)
{
    public static CharacterAvatarItem From(RpCharacter character, GalleryImage? image) =>
        new(
            character.Id,
            character.Name,
            image?.Url,
            image is null ? ImageAvatarCropView.Default : ImageAvatarCropView.From(image));
}
