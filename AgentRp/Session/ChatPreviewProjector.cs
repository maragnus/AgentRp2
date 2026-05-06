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
        chat.Starred = document.Chat.Starred;
        ApplySceneCharacters(chat, document.Characters, document.Images, new HashSet<string>(StringComparer.Ordinal));
    }

    public static void ApplySceneCharacters(
        RpChat chat,
        IEnumerable<RpCharacter> characters,
        IEnumerable<GalleryImage> images,
        IReadOnlySet<string> sceneCharacterIds)
    {
        var imagesById = images
            .Where(image => !string.IsNullOrWhiteSpace(image.Id))
            .GroupBy(image => image.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

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
