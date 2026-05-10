namespace AgentRp.Components.Entities;

public sealed class CharacterRelationshipSummaryItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string TargetName { get; set; } = "";
    public string Metadata { get; set; } = "";
    public IReadOnlyCollection<string> RelationshipTypes { get; set; } = [];
    public IReadOnlyCollection<string> PrivateTensions { get; set; } = [];
    public string PublicDynamic { get; set; } = "";
    public string HowSourceSeesTarget { get; set; } = "";
    public string HowTargetSeesSource { get; set; } = "";
}
