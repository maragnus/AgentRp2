using AgentRp.Models;

namespace AgentRp.Components.Chat;

public sealed class SnapshotDraftTimelineEntryEditor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string WhenText { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
    public List<string> CharacterNames { get; set; } = [];
    public List<string> LocationNames { get; set; } = [];
    public List<string> ItemNames { get; set; } = [];

    public static SnapshotDraftTimelineEntryEditor From(RpTranscriptSnapshotTimelineEntry entry) => new()
    {
        WhenText = entry.WhenText,
        Title = entry.Title,
        Summary = entry.Summary,
        Details = entry.Details,
        CharacterNames = [.. entry.CharacterNames],
        LocationNames = [.. entry.LocationNames],
        ItemNames = [.. entry.ItemNames]
    };

    public RpTranscriptSnapshotTimelineEntry ToModel() => new()
    {
        WhenText = WhenText,
        Title = Title,
        Summary = Summary,
        Details = Details,
        CharacterNames = [.. CharacterNames],
        LocationNames = [.. LocationNames],
        ItemNames = [.. ItemNames]
    };
}
