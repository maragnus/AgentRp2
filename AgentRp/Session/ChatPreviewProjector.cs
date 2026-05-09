using AgentRp.Models;

namespace AgentRp.Session;

static class ChatPreviewProjector
{
    public static void Apply(RpChat chat, RpChatDocument document)
    {
        chat.Title = document.Chat.Title;
        chat.Location = document.Chat.Location;
        chat.Messages = document.Chat.Messages;
        chat.Updated = document.Chat.Updated;
        chat.LastMessageUtc = document.Chat.LastMessageUtc;
        chat.LastGeneratedTurnNumber = document.Chat.LastGeneratedTurnNumber;
        chat.Starred = document.Chat.Starred;
        var scene = TranscriptGraph.GetVisibleScene(document.Transcript);
        var sceneCharacterIds = scene.InSceneCharacterIds.ToHashSet(StringComparer.Ordinal);
        var imagesById = ImageLookup(document.Images);
        ApplyActiveLocation(chat, document.Locations, imagesById, scene.LocationId, scene.LocationName);
        ApplySceneCharacters(chat, document.Characters, imagesById, sceneCharacterIds);
    }

    public static void ApplySceneCharacters(
        RpChat chat,
        IEnumerable<RpCharacter> characters,
        IEnumerable<GalleryImage> images,
        IReadOnlySet<string> sceneCharacterIds) =>
        ApplySceneCharacters(chat, characters, ImageLookup(images), sceneCharacterIds);

    static void ApplySceneCharacters(
        RpChat chat,
        IEnumerable<RpCharacter> characters,
        IReadOnlyDictionary<string, GalleryImage> imagesById,
        IReadOnlySet<string> sceneCharacterIds)
    {
        chat.SceneCharacters = characters
            .Where(character => IsSceneCharacter(character, sceneCharacterIds))
            .Select(character => new RpChatSceneCharacter
            {
                Id = character.Id,
                Name = character.Name,
                ImageId = character.ImageId,
                Image = imagesById.GetValueOrDefault(character.ImageId)
            })
            .ToList();
    }

    static void ApplyActiveLocation(
        RpChat chat,
        IEnumerable<RpLocation> locations,
        IReadOnlyDictionary<string, GalleryImage> imagesById,
        string locationId,
        string locationName)
    {
        var location = locations.FirstOrDefault(location => location.Id == locationId)
            ?? locations.FirstOrDefault(location => location.IsActive)
            ?? locations.FirstOrDefault();
        var name = !string.IsNullOrWhiteSpace(locationName) ? locationName : location?.Name ?? chat.Location;
        chat.Location = name;
        chat.ActiveLocation = string.IsNullOrWhiteSpace(location?.Id) && string.IsNullOrWhiteSpace(name)
            ? null
            : new()
            {
                Id = location?.Id ?? locationId,
                Name = name,
                ImageId = location?.ImageId ?? "",
                Image = location is null ? null : imagesById.GetValueOrDefault(location.ImageId)
            };
    }

    static Dictionary<string, GalleryImage> ImageLookup(IEnumerable<GalleryImage> images) =>
        images
            .Where(image => !string.IsNullOrWhiteSpace(image.Id))
            .GroupBy(image => image.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    static bool IsSceneCharacter(RpCharacter character, IReadOnlySet<string> sceneCharacterIds)
    {
        if (character.Id.Equals("__narrator", StringComparison.OrdinalIgnoreCase)
            || character.Name.Equals("Narrator", StringComparison.OrdinalIgnoreCase))
            return false;

        return sceneCharacterIds.Count > 0
            ? sceneCharacterIds.Contains(character.Id)
            : character.InScene;
    }
}
