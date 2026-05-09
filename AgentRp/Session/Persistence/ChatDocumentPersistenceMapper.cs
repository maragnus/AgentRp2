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
    PromptLibraryStateRow? PromptLibrary,
    CharacterTraitLibraryStateRow? CharacterTraitLibrary,
    ModelTuningStateRow? ModelTuning);

internal static class ChatDocumentPersistenceMapper
{
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
            PromptLibrary = DeserializeState(rows.PromptLibrary, PromptLibraryState.CreateDefault()),
            CharacterTraitLibrary = DeserializeState(rows.CharacterTraitLibrary, CharacterTraitLibraryState.CreateDefault()),
            ModelTuning = DeserializeState(rows.ModelTuning, ModelTuningState.CreateDefault())
        };

        TranscriptProjector.Apply(document);
        return document;
    }

    public static RpTranscriptState ToTranscriptShell(ChatTranscriptStateRow? row, string activeLeafTurnId)
    {
        if (row is null)
            return new() { ActiveLeafTurnId = activeLeafTurnId };

        return new()
        {
            SchemaVersion = row.SchemaVersion,
            RootScene = PersistenceJson.Deserialize(row.RootSceneJson, new RpSceneFrame()),
            WorkingScene = PersistenceJson.Deserialize(row.WorkingSceneJson, new RpWorkingSceneState()),
            Options = PersistenceJson.Deserialize(row.OptionsJson, new RpTranscriptOptionsState()),
            ActiveLeafTurnId = activeLeafTurnId,
            BranchSelections = PersistenceJson.Deserialize(row.BranchSelectionsJson, new Dictionary<string, string>(StringComparer.Ordinal)),
            Data = PersistenceJson.Deserialize(row.DataJson, new JsonObject())
        };
    }

    public static void ApplyTranscriptShell(RpTranscriptState transcript, ChatTranscriptStateRow row, DateTime now)
    {
        row.SchemaVersion = transcript.SchemaVersion;
        row.RootSceneJson = PersistenceJson.Serialize(transcript.RootScene);
        row.WorkingSceneJson = PersistenceJson.Serialize(transcript.WorkingScene);
        row.OptionsJson = PersistenceJson.Serialize(transcript.Options);
        row.BranchSelectionsJson = PersistenceJson.Serialize(transcript.BranchSelections);
        row.DataJson = PersistenceJson.Serialize(transcript.Data);
        row.UpdatedUtc = now;
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

    public static void Apply(PromptLibraryState state, PromptLibraryStateRow row, DateTime now)
    {
        row.StateJson = PersistenceJson.Serialize(PromptLibraryService.CreateOverridesFromResolved(state));
        row.UpdatedUtc = now;
    }

    public static void Apply(CharacterTraitLibraryState state, CharacterTraitLibraryStateRow row, DateTime now)
    {
        row.StateJson = PersistenceJson.Serialize(state);
        row.UpdatedUtc = now;
    }

    public static void Apply(ModelTuningState state, ModelTuningStateRow row, DateTime now)
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
            PromptLibraryStateRow value => value.StateJson,
            CharacterTraitLibraryStateRow value => value.StateJson,
            ModelTuningStateRow value => value.StateJson,
            _ => ""
        };
        return PersistenceJson.Deserialize(json, fallback);
    }
}
