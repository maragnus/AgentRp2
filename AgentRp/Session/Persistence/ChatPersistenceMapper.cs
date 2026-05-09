using AgentRp.Data;
using AgentRp.Models;

namespace AgentRp.Session;

internal static class ChatPersistenceMapper
{
    public static RpChat ToModel(RpChatRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        Updated = row.Updated,
        LastMessageUtc = row.LastMessageUtc,
        LastGeneratedTurnNumber = row.LastGeneratedTurnNumber,
        Starred = row.Starred,
        Messages = row.ActiveTurnCount > 0 ? row.ActiveTurnCount : row.Messages,
        Location = string.IsNullOrWhiteSpace(row.ActiveLocationName) ? row.Location : row.ActiveLocationName,
        ActiveLocation = PersistenceJson.Deserialize(row.ActiveLocationJson, (RpChatSceneLocation?)null) ?? FallbackLocation(row),
        SceneCharacters = PersistenceJson.Deserialize(row.SceneCharactersJson, new List<RpChatSceneCharacter>())
    };

    public static void Apply(RpChat chat, RpChatRow row, int sortOrder, DateTime now)
    {
        row.Title = chat.Title;
        row.Updated = chat.Updated;
        row.LastMessageUtc = chat.LastMessageUtc;
        row.LastGeneratedTurnNumber = chat.LastGeneratedTurnNumber;
        row.Starred = chat.Starred;
        row.Messages = chat.Messages;
        row.Location = chat.Location;
        row.ActiveLocationJson = PersistenceJson.Serialize(chat.ActiveLocation);
        row.SceneCharactersJson = PersistenceJson.Serialize(chat.SceneCharacters);
        row.SortOrder = sortOrder;
        row.UpdatedUtc = now;
    }

    public static void ApplyTranscriptPreview(RpChatDocument document, RpChatRow row)
    {
        row.ActiveLeafTurnId = document.Transcript.ActiveLeafTurnId;
        row.ActiveTurnCount = document.Chat.Messages;
        row.ActiveLocationId = document.Chat.ActiveLocation?.Id ?? "";
        row.ActiveLocationName = document.Chat.ActiveLocation?.Name ?? document.Chat.Location;
        row.SnapshotCount = document.Transcript.Snapshots.Count;
    }

    static RpChatSceneLocation? FallbackLocation(RpChatRow row) =>
        string.IsNullOrWhiteSpace(row.Location)
            ? null
            : new() { Name = row.Location };
}
