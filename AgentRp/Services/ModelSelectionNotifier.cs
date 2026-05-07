using AgentRp.Models;

namespace AgentRp.Services;

public static class ModelSelectionChangeKinds
{
    public const string Selected = "selected";
    public const string Fallback = "fallback";
    public const string Cleared = "cleared";
}

public sealed record ModelSelectionChangeNotification(
    AiModelRole Role,
    string ProviderId,
    string ModelId,
    string ChangeKind);

public interface IModelSelectionNotifier
{
    event Func<ModelSelectionChangeNotification, Task>? Changed;
    Task PublishAsync(ModelSelectionChangeNotification notification);
}

public sealed class ModelSelectionNotifier : IModelSelectionNotifier
{
    public event Func<ModelSelectionChangeNotification, Task>? Changed;

    public async Task PublishAsync(ModelSelectionChangeNotification notification)
    {
        var handlers = Changed?
            .GetInvocationList()
            .Cast<Func<ModelSelectionChangeNotification, Task>>()
            .ToList();
        if (handlers is null)
            return;

        foreach (var handler in handlers)
            await handler(notification);
    }
}

public sealed class NullModelSelectionNotifier : IModelSelectionNotifier
{
    public static NullModelSelectionNotifier Instance { get; } = new();

    public event Func<ModelSelectionChangeNotification, Task>? Changed
    {
        add { }
        remove { }
    }

    public Task PublishAsync(ModelSelectionChangeNotification notification) => Task.CompletedTask;
}
