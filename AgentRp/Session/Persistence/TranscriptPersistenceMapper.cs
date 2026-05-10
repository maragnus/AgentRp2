using System.Text.Json.Nodes;
using AgentRp.Data;
using AgentRp.Models;

namespace AgentRp.Session;

internal static class TranscriptPersistenceMapper
{
    public static RpTranscriptState ToShell(RpTranscriptState transcript) => new()
    {
        SchemaVersion = transcript.SchemaVersion,
        RootScene = SessionCloner.Clone(transcript.RootScene),
        WorkingScene = SessionCloner.Clone(transcript.WorkingScene),
        Options = SessionCloner.Clone(transcript.Options),
        ActiveLeafTurnId = transcript.ActiveLeafTurnId,
        BranchSelections = transcript.BranchSelections.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        Data = (JsonObject?)transcript.Data.DeepClone() ?? new()
    };

    public static RpTranscriptTurn ToModel(TranscriptTurnRow row) => new()
    {
        Id = row.Id,
        ParentTurnId = row.ParentTurnId,
        TurnNumber = row.TurnNumber,
        CreatedUtc = row.CreatedUtc,
        UpdatedUtc = row.UpdatedUtc,
        Mode = row.Mode,
        AuthorCharacterId = row.AuthorCharacterId,
        AuthorName = row.AuthorName,
        ActorCharacterId = row.ActorCharacterId,
        ActorName = row.ActorName,
        Guidance = row.Guidance,
        Body = row.Body,
        Plan = PersistenceJson.Deserialize(row.PlanJson, new RpTurnPlan()),
        AppearanceByCharacterId = PersistenceJson.Deserialize(row.AppearanceJson, new Dictionary<string, string>(StringComparer.Ordinal)),
        PrivateIntentByCharacterId = PersistenceJson.Deserialize(row.PrivateIntentJson, new Dictionary<string, string>(StringComparer.Ordinal)),
        SnapshotId = row.ConsumedBySnapshotId,
        ConsumedBySnapshotOrdinal = row.ConsumedBySnapshotOrdinal,
        Speech = PersistenceJson.Deserialize(row.SpeechJson, new RpMessageSpeechState()),
        Scene = PersistenceJson.Deserialize(row.SceneJson, new RpSceneFrame { LocationId = row.SceneLocationId, LocationName = row.SceneLocationName }),
        Trace = PersistenceJson.Deserialize(row.TraceJson, (RpGenerationTrace?)null)
    };

    public static RpTranscriptSnapshot ToModel(TranscriptSnapshotRow row) => new()
    {
        Id = row.Id,
        TurnId = string.IsNullOrWhiteSpace(row.TurnId) ? row.EndTurnId : row.TurnId,
        StartTurnId = row.StartTurnId,
        EndTurnId = row.EndTurnId,
        ParentBeforeStartTurnId = row.ParentBeforeStartTurnId,
        TurnNumberStart = row.TurnNumberStart,
        TurnNumberEnd = row.TurnNumberEnd,
        CreatedUtc = row.CreatedUtc,
        UpdatedUtc = row.UpdatedUtc,
        Summary = row.Summary,
        Speech = PersistenceJson.Deserialize(row.SpeechJson, new RpMessageSpeechState()),
        PrivateIntentByCharacterId = PersistenceJson.Deserialize(row.PrivateIntentJson, new Dictionary<string, string>(StringComparer.Ordinal)),
        CharacterAppearances = PersistenceJson.Deserialize(row.CharacterAppearancesJson, new Dictionary<string, string>(StringComparer.Ordinal)),
        RelationshipUpdates = PersistenceJson.Deserialize(row.RelationshipUpdatesJson, new List<RpTranscriptSnapshotRelationshipUpdate>()),
        Scene = PersistenceJson.Deserialize(row.SceneJson, new RpSceneFrame { LocationId = row.SceneLocationId, LocationName = row.SceneLocationName }),
        Trace = PersistenceJson.Deserialize(row.TraceJson, (RpGenerationTrace?)null),
        ConsumedBySnapshotId = row.ConsumedBySnapshotId,
        ConsumedBySnapshotOrdinal = row.ConsumedBySnapshotOrdinal,
        IsActive = row.IsActive
    };

    public static void Apply(RpTranscriptTurn turn, TranscriptTurnRow row)
    {
        row.ParentTurnId = turn.ParentTurnId;
        row.TurnNumber = turn.TurnNumber;
        row.CreatedUtc = turn.CreatedUtc;
        row.UpdatedUtc = turn.UpdatedUtc;
        row.Mode = turn.Mode;
        row.AuthorCharacterId = turn.AuthorCharacterId;
        row.AuthorName = turn.AuthorName;
        row.ActorCharacterId = turn.ActorCharacterId;
        row.ActorName = turn.ActorName;
        row.Guidance = turn.Guidance;
        row.Body = turn.Body;
        row.SceneLocationId = turn.Scene.LocationId;
        row.SceneLocationName = turn.Scene.LocationName;
        row.SceneJson = PersistenceJson.Serialize(turn.Scene);
        row.PlanJson = PersistenceJson.Serialize(turn.Plan);
        row.AppearanceJson = PersistenceJson.Serialize(turn.AppearanceByCharacterId);
        row.PrivateIntentJson = PersistenceJson.Serialize(turn.PrivateIntentByCharacterId);
        row.SpeechJson = PersistenceJson.Serialize(turn.Speech);
        row.TraceJson = turn.Trace is null ? "" : PersistenceJson.Serialize(turn.Trace);
        row.ConsumedBySnapshotId = turn.SnapshotId;
        row.ConsumedBySnapshotOrdinal = turn.ConsumedBySnapshotOrdinal;
    }

    public static void Apply(RpTranscriptSnapshot snapshot, TranscriptSnapshotRow row)
    {
        row.TurnId = snapshot.TurnId;
        row.StartTurnId = snapshot.StartTurnId;
        row.EndTurnId = string.IsNullOrWhiteSpace(snapshot.EndTurnId) ? snapshot.TurnId : snapshot.EndTurnId;
        row.ParentBeforeStartTurnId = snapshot.ParentBeforeStartTurnId;
        row.TurnNumberStart = snapshot.TurnNumberStart;
        row.TurnNumberEnd = snapshot.TurnNumberEnd;
        row.CreatedUtc = snapshot.CreatedUtc;
        row.UpdatedUtc = snapshot.UpdatedUtc == default ? snapshot.CreatedUtc : snapshot.UpdatedUtc;
        row.Summary = snapshot.Summary;
        row.SceneLocationId = snapshot.Scene.LocationId;
        row.SceneLocationName = snapshot.Scene.LocationName;
        row.SceneJson = PersistenceJson.Serialize(snapshot.Scene);
        row.SpeechJson = PersistenceJson.Serialize(snapshot.Speech);
        row.PrivateIntentJson = PersistenceJson.Serialize(snapshot.PrivateIntentByCharacterId);
        row.CharacterAppearancesJson = PersistenceJson.Serialize(snapshot.CharacterAppearances);
        row.RelationshipUpdatesJson = PersistenceJson.Serialize(snapshot.RelationshipUpdates);
        row.TraceJson = snapshot.Trace is null ? "" : PersistenceJson.Serialize(snapshot.Trace);
        row.ConsumedBySnapshotId = snapshot.ConsumedBySnapshotId;
        row.ConsumedBySnapshotOrdinal = snapshot.ConsumedBySnapshotOrdinal;
        row.IsActive = snapshot.IsActive;
    }
}
