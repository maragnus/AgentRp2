using AgentRp.Models;

namespace AgentRp.Components.Chat;

public sealed class SnapshotDraftTimelineEntryEditor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int TurnNumber { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> CharacterNames { get; set; } = [];
    public List<string> LocationNames { get; set; } = [];
    public List<string> ItemNames { get; set; } = [];

    public static SnapshotDraftTimelineEntryEditor From(RpTranscriptSnapshotTimelineEntry entry) => new()
    {
        TurnNumber = entry.TurnNumber,
        Title = entry.Title,
        Description = entry.Description,
        CharacterNames = [.. entry.CharacterNames],
        LocationNames = [.. entry.LocationNames],
        ItemNames = [.. entry.ItemNames]
    };

    public RpTranscriptSnapshotTimelineEntry ToModel() => new()
    {
        TurnNumber = TurnNumber,
        Title = Title,
        Description = Description,
        CharacterNames = [.. CharacterNames],
        LocationNames = [.. LocationNames],
        ItemNames = [.. ItemNames]
    };
}
