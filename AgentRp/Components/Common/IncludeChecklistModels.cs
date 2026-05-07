namespace AgentRp.Components.Common;

public sealed record IncludeChecklistItem(string Id, string Label, string Icon, string Note, string Count = "");

public sealed record IncludeChecklistRowContext(IncludeChecklistItem Item, bool Selected, bool Indeterminate);
