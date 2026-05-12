using AgentRp.Models;

namespace AgentRp.Components.Chat;

public sealed class SnapshotDraftTimelineEntryEditor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int TurnNumber { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> CharacterIds { get; set; } = [];
    public List<string> LocationIds { get; set; } = [];
    public List<string> ItemNames { get; set; } = [];

    public static SnapshotDraftTimelineEntryEditor From(RpTranscriptSnapshotTimelineEntry entry, int index) => new()
    {
        Id = $"timeline-{index}",
        TurnNumber = entry.TurnNumber,
        Title = entry.Title,
        Description = entry.Description,
        CharacterIds = [.. entry.CharacterIds],
        LocationIds = [.. entry.LocationIds],
        ItemNames = [.. entry.ItemNames]
    };

    public RpTranscriptSnapshotTimelineEntry ToModel() => new()
    {
        TurnNumber = TurnNumber,
        Title = Title,
        Description = Description,
        CharacterIds = [.. CharacterIds],
        LocationIds = [.. LocationIds],
        ItemNames = [.. ItemNames]
    };
}

public sealed class SnapshotDraftEditorState
{
    public string Summary { get; set; } = "";
    public List<SnapshotDraftTimelineEntryEditor> TimelineEntries { get; set; } = [];
    public List<RpTranscriptSnapshotRelationshipUpdate> RelationshipUpdates { get; set; } = [];

    public static SnapshotDraftEditorState From(RpTranscriptSnapshotDraft draft) => new()
    {
        Summary = draft.Summary,
        TimelineEntries = draft.TimelineEntries.Select(SnapshotDraftTimelineEntryEditor.From).ToList(),
        RelationshipUpdates = draft.RelationshipUpdates.Select(CloneRelationshipUpdate).ToList()
    };

    public static RpTranscriptSnapshotRelationshipUpdate CloneRelationshipUpdate(RpTranscriptSnapshotRelationshipUpdate value) => new()
    {
        RelationshipId = value.RelationshipId,
        ApplyChange = value.ApplyChange,
        SourceCharacterId = value.SourceCharacterId,
        TargetCharacterId = value.TargetCharacterId,
        RelationshipTypes = [.. value.RelationshipTypes],
        PrivateTensions = [.. value.PrivateTensions],
        HowSourceSeesTarget = value.HowSourceSeesTarget,
        HowTargetSeesSource = value.HowTargetSeesSource,
        PublicDynamic = value.PublicDynamic,
        Reason = value.Reason,
        EvidenceTurnNumbers = [.. value.EvidenceTurnNumbers]
    };
}
