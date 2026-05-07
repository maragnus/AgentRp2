using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Components.Common;

public sealed record ActiveModelPickerContext(
    bool IsOpen,
    AiModelRole Role,
    ActiveModelSelection? ActiveModel,
    Func<Task> Open,
    Func<Task> Close,
    Func<Task> Toggle);
