using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public static class StoryPreviewProjector
{
    public static StoryPreview FromDocument(RpChatDocument document)
    {
        var scene = TranscriptGraph.GetVisibleScene(document.Transcript);
        var sceneCharacterIds = scene.InSceneCharacterIds.ToHashSet(StringComparer.Ordinal);
        var imagesById = document.Images
            .Where(image => !string.IsNullOrWhiteSpace(image.Id))
            .GroupBy(image => image.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var location = document.Locations.FirstOrDefault(location => location.Id == scene.LocationId)
            ?? document.Locations.FirstOrDefault(location => location.IsActive)
            ?? document.Locations.FirstOrDefault();
        var locationName = location?.Name
            ?? (!string.IsNullOrWhiteSpace(scene.LocationName) ? scene.LocationName : document.Chat.Location);

        return new()
        {
            ChatId = document.Chat.Id,
            Title = document.Chat.Title,
            Starred = document.Chat.Starred,
            VisibleTurnCount = document.Chat.Messages,
            LastGeneratedTurnNumber = document.Chat.LastGeneratedTurnNumber,
            LastMessageUtc = document.Chat.LastMessageUtc,
            Updated = document.Chat.Updated,
            ActiveLocation = string.IsNullOrWhiteSpace(locationName) && string.IsNullOrWhiteSpace(location?.Id)
                ? null
                : new()
                {
                    LocationId = location?.Id ?? scene.LocationId,
                    Name = locationName,
                    Avatar = AvatarFor(location?.ImageId, imagesById)
                },
            SceneCharacters = document.Characters
                .Where(character => IsSceneCharacter(character, sceneCharacterIds))
                .Select(character => new StoryPreviewCharacter
                {
                    CharacterId = character.Id,
                    Name = character.Name,
                    Avatar = AvatarFor(character.ImageId, imagesById)
                })
                .ToList()
        };
    }

    public static RpChat ToChat(StoryPreview preview) => new()
    {
        Id = preview.ChatId,
        Title = preview.Title,
        Starred = preview.Starred,
        Messages = preview.VisibleTurnCount,
        LastGeneratedTurnNumber = preview.LastGeneratedTurnNumber,
        LastMessageUtc = preview.LastMessageUtc,
        Updated = preview.Updated,
        Location = preview.ActiveLocation?.Name ?? "",
        ActiveLocation = preview.ActiveLocation is null
            ? null
            : new()
            {
                Id = preview.ActiveLocation.LocationId,
                Name = preview.ActiveLocation.Name,
                ImageId = preview.ActiveLocation.Avatar?.ImageId ?? "",
                Image = ToGalleryImage(preview.ActiveLocation.Avatar)
            },
        SceneCharacters = preview.SceneCharacters.Select(character => new RpChatSceneCharacter
        {
            Id = character.CharacterId,
            Name = character.Name,
            ImageId = character.Avatar?.ImageId ?? "",
            Image = ToGalleryImage(character.Avatar)
        }).ToList()
    };

    static StoryPreviewAvatar? AvatarFor(string? imageId, IReadOnlyDictionary<string, GalleryImage> imagesById)
    {
        if (string.IsNullOrWhiteSpace(imageId) || !imagesById.TryGetValue(imageId, out var image))
            return null;

        return new()
        {
            ImageId = image.Id,
            Url = image.Url,
            FocusXPercent = image.AvatarFocusXPercent,
            FocusYPercent = image.AvatarFocusYPercent,
            ZoomPercent = image.AvatarZoomPercent
        };
    }

    static GalleryImage? ToGalleryImage(StoryPreviewAvatar? avatar) =>
        avatar is null
            ? null
            : new()
            {
                Id = avatar.ImageId,
                Url = avatar.Url,
                AvatarFocusXPercent = avatar.FocusXPercent,
                AvatarFocusYPercent = avatar.FocusYPercent,
                AvatarZoomPercent = avatar.ZoomPercent
            };

    static bool IsSceneCharacter(RpCharacter character, IReadOnlySet<string> sceneCharacterIds)
    {
        if (character.Id.Equals(EntityIds.Narrator, StringComparison.OrdinalIgnoreCase)
            || character.Name.Equals("Narrator", StringComparison.OrdinalIgnoreCase))
            return false;

        return sceneCharacterIds.Count > 0
            ? sceneCharacterIds.Contains(character.Id)
            : character.InScene;
    }
}
