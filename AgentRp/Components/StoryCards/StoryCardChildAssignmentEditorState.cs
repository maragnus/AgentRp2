using AgentRp.Components.Common;

namespace AgentRp.Components.StoryCards;

public sealed record StoryCardChildAssignmentEditorState(
    string Label,
    IReadOnlyList<EntityTagPickerOption> Options,
    IReadOnlyCollection<string> SelectedIds,
    string EmptyText,
    string EmptyPickerText,
    string AddTooltipText,
    bool Dirty,
    Func<string, Task> ToggleAsync);
