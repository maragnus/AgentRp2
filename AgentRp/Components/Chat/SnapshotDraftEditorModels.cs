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

    public static SnapshotDraftTimelineEntryEditor From(RpTranscriptSnapshotTimelineEntry entry) => new()
    {
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
