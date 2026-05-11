using System.Text.Json.Nodes;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

internal sealed record ChatDocumentRows(
    RpChatRow? Chat,
    List<ChatCharacterRow> Characters,
    List<ChatCharacterRelationshipRow> CharacterRelationships,
    List<ChatLocationRow> Locations,
    List<ChatItemRow> Items,
    List<ChatTimelineEntryRow> Timeline,
    List<ImageAssetRow> Images,
    ChatTranscriptStateRow? TranscriptState,
    ChatDirectionStateRow? ChatDirection,
    NarratorProfileStateRow? NarratorProfile,
    CharacterTraitLibraryStateRow? CharacterTraitLibrary,
    List<StoryModelSelectionRow> ModelSelections);

internal static class ChatDocumentPersistenceMapper
{
    const string CyoaDataKey = "cyoa";

    public static RpChatDocument CreateEmpty(string chatId, RpChatRow? chat) => new()
    {
        Chat = chat is null ? new RpChat { Id = chatId } : ChatPersistenceMapper.ToModel(chat)
    };

    public static RpChatDocument ToModel(ChatDocumentRows rows, RpTranscriptState transcript)
    {
        var document = new RpChatDocument
        {
            Chat = rows.Chat is null ? new() : ChatPersistenceMapper.ToModel(rows.Chat),
            Characters = rows.Characters
                .OrderBy(row => row.SortOrder)
                .Select(StoryEntityPersistenceMapper.ToModel)
                .ToList(),
            CharacterRelationships = rows.CharacterRelationships
                .OrderBy(row => row.SortOrder)
                .Select(StoryEntityPersistenceMapper.ToModel)
                .ToList(),
            Locations = rows.Locations
                .OrderBy(row => row.SortOrder)
                .Select(StoryEntityPersistenceMapper.ToModel)
                .ToList(),
            Items = rows.Items
                .OrderBy(row => row.SortOrder)
                .Select(StoryEntityPersistenceMapper.ToModel)
                .ToList(),
            Timeline = rows.Timeline
                .OrderBy(row => row.SortOrder)
                .Select(StoryEntityPersistenceMapper.ToModel)
                .ToList(),
            Images = rows.Images
                .OrderBy(row => row.SortOrder)
                .ThenByDescending(row => row.CreatedUtc)
                .Select(StoryEntityPersistenceMapper.ToModel)
                .ToList(),
            Transcript = transcript,
            StoryAssistant = new(),
            ChatDirection = DeserializeState(rows.ChatDirection, ChatDirectionState.CreateDefault()),
            NarratorProfile = DeserializeState(rows.NarratorProfile, NarratorProfileState.CreateDefault()),
            CharacterTraitLibrary = DeserializeState(rows.CharacterTraitLibrary, CharacterTraitLibraryState.CreateDefault()),
            ModelSelections = ToModelSelections(rows.ModelSelections)
        };

        if (rows.ChatDirection is not null && document.ChatDirection.UpdatedUtc == default)
            document.ChatDirection.UpdatedUtc = rows.ChatDirection.UpdatedUtc;

        if (rows.CharacterTraitLibrary is not null && document.CharacterTraitLibrary.UpdatedUtc == default)
            document.CharacterTraitLibrary.UpdatedUtc = rows.CharacterTraitLibrary.UpdatedUtc;

        TranscriptProjector.Apply(document);
        return document;
    }

    public static RpTranscriptState ToTranscriptShell(ChatTranscriptStateRow? row, string activeLeafTurnId)
    {
        if (row is null)
            return new() { ActiveLeafTurnId = activeLeafTurnId };

        var data = PersistenceJson.Deserialize(row.DataJson, new JsonObject());
        var cyoa = DeserializeCyoa(data);
        data.Remove(CyoaDataKey);
        return new()
        {
            SchemaVersion = row.SchemaVersion,
            RootScene = PersistenceJson.Deserialize(row.RootSceneJson, new RpSceneFrame()),
            WorkingScene = PersistenceJson.Deserialize(row.WorkingSceneJson, new RpWorkingSceneState()),
            Options = PersistenceJson.Deserialize(row.OptionsJson, new RpTranscriptOptionsState()),
            ActiveLeafTurnId = activeLeafTurnId,
            BranchSelections = PersistenceJson.Deserialize(row.BranchSelectionsJson, new Dictionary<string, string>(StringComparer.Ordinal)),
            Cyoa = cyoa,
            Data = data
        };
    }

    public static void ApplyTranscriptShell(RpTranscriptState transcript, ChatTranscriptStateRow row, DateTime now)
    {
        row.SchemaVersion = transcript.SchemaVersion;
        row.RootSceneJson = PersistenceJson.Serialize(transcript.RootScene);
        row.WorkingSceneJson = PersistenceJson.Serialize(transcript.WorkingScene);
        row.OptionsJson = PersistenceJson.Serialize(transcript.Options);
        row.BranchSelectionsJson = PersistenceJson.Serialize(transcript.BranchSelections);
        row.DataJson = PersistenceJson.Serialize(DataWithCyoa(transcript));
        row.UpdatedUtc = now;
    }

    static RpCyoaState DeserializeCyoa(JsonObject data)
    {
        return data.TryGetPropertyValue(CyoaDataKey, out var node) && node is not null
            ? PersistenceJson.Deserialize(node.ToJsonString(), new RpCyoaState())
            : new();
    }

    static JsonObject DataWithCyoa(RpTranscriptState transcript)
    {
        var data = (JsonObject?)transcript.Data.DeepClone() ?? new();
        data[CyoaDataKey] = JsonNode.Parse(PersistenceJson.Serialize(transcript.Cyoa));
        return data;
    }

    public static void Apply(ChatDirectionState state, ChatDirectionStateRow row, DateTime now)
    {
        row.StateJson = PersistenceJson.Serialize(ChatDirectionService.NormalizeState(state));
        row.UpdatedUtc = now;
    }

    public static void Apply(NarratorProfileState state, NarratorProfileStateRow row, DateTime now)
    {
        row.StateJson = PersistenceJson.Serialize(NarratorProfileService.NormalizeState(state));
        row.UpdatedUtc = now;
    }

    public static void Apply(CharacterTraitLibraryState state, CharacterTraitLibraryStateRow row, DateTime now)
    {
        row.StateJson = PersistenceJson.Serialize(state);
        row.UpdatedUtc = now;
    }

    static TState DeserializeState<TRow, TState>(TRow? row, TState fallback)
        where TRow : class
    {
        var json = row switch
        {
            ChatDirectionStateRow value => value.StateJson,
            NarratorProfileStateRow value => value.StateJson,
            CharacterTraitLibraryStateRow value => value.StateJson,
            _ => ""
        };
        return PersistenceJson.Deserialize(json, fallback);
    }

    static ActiveModelSelectionsState ToModelSelections(IEnumerable<StoryModelSelectionRow> rows)
    {
        var state = ActiveModelSelectionsState.CreateDefault();
        foreach (var row in rows)
        {
            if (!Enum.TryParse<AiModelRole>(row.Role, out var role))
                continue;

            state.Values[role] = new()
            {
                ProviderId = row.ProviderId,
                ModelId = row.ModelId
            };
        }

        return state;
    }
}
