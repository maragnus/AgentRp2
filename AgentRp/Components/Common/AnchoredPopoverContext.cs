namespace AgentRp.Components.Common;

public sealed record AnchoredPopoverContext(
    bool IsOpen,
    Func<Task> Open,
    Func<Task> Close,
    Func<Task> Toggle);
