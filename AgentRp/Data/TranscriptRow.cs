namespace AgentRp.Data;

public sealed class TranscriptTurnRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string ParentTurnId { get; set; } = "";
    public int TurnNumber { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string Mode { get; set; } = "";
    public string AuthorCharacterId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string ActorCharacterId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string Guidance { get; set; } = "";
    public string Body { get; set; } = "";
    public string SceneLocationId { get; set; } = "";
    public string SceneLocationName { get; set; } = "";
    public string SceneJson { get; set; } = "";
    public string PlanJson { get; set; } = "";
    public string AppearanceJson { get; set; } = "{}";
    public string PrivateIntentJson { get; set; } = "{}";
    public string SpeechJson { get; set; } = "";
    public string TraceJson { get; set; } = "";
    public string ConsumedBySnapshotId { get; set; } = "";
    public int? ConsumedBySnapshotOrdinal { get; set; }
}

public sealed class TranscriptSnapshotRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string TurnId { get; set; } = "";
    public string StartTurnId { get; set; } = "";
    public string EndTurnId { get; set; } = "";
    public string ParentBeforeStartTurnId { get; set; } = "";
    public int TurnNumberStart { get; set; }
    public int TurnNumberEnd { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string Summary { get; set; } = "";
    public string SceneLocationId { get; set; } = "";
    public string SceneLocationName { get; set; } = "";
    public string SceneJson { get; set; } = "";
    public string SpeechJson { get; set; } = "";
    public string PrivateIntentJson { get; set; } = "{}";
    public string CharacterAppearancesJson { get; set; } = "{}";
    public string TraceJson { get; set; } = "";
    public string ConsumedBySnapshotId { get; set; } = "";
    public int? ConsumedBySnapshotOrdinal { get; set; }
    public bool IsActive { get; set; } = true;
}
