namespace AgentRp.Components.Common;

public sealed record EntityTagPickerOption(string EntityType, string EntityId, string Name)
{
    public string Key => $"{EntityType}:{EntityId}";
}
