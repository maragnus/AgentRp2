namespace AgentRp.Services;

public static class EntityTypes
{
    public const string Character = "character";
    public const string Location = "location";
    public const string Item = "item";
    public const string Narrator = "narrator";
    public const string Snapshot = "snapshot";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "characters" => Character,
        "character" => Character,
        "locations" => Location,
        "location" => Location,
        "items" => Item,
        "item" => Item,
        "narrator" => Narrator,
        "snapshots" => Snapshot,
        "snapshot" => Snapshot,
        _ => ""
    };
}

public static class EntityIds
{
    public const string Narrator = "__narrator";
}

public static class EntityChangeKinds
{
    public const string Profile = "profile";
    public const string Image = "image";
    public const string ImageCrop = "imageCrop";
    public const string Deleted = "deleted";
}

public sealed record EntityChangeNotification(
    string EntityType,
    string EntityId,
    string ChangeKind,
    string ImageId = "",
    string ChatId = "");

public interface IEntityNotifier
{
    event Func<EntityChangeNotification, Task>? Changed;
    Task PublishAsync(EntityChangeNotification notification);
}

public sealed class EntityNotifier : IEntityNotifier
{
    public event Func<EntityChangeNotification, Task>? Changed;

    public async Task PublishAsync(EntityChangeNotification notification)
    {
        var handlers = Changed?
            .GetInvocationList()
            .Cast<Func<EntityChangeNotification, Task>>()
            .ToList();
        if (handlers is null)
            return;

        foreach (var handler in handlers)
            await handler(notification);
    }
}

public sealed class NullEntityNotifier : IEntityNotifier
{
    public static NullEntityNotifier Instance { get; } = new();

    public event Func<EntityChangeNotification, Task>? Changed
    {
        add { }
        remove { }
    }

    public Task PublishAsync(EntityChangeNotification notification) => Task.CompletedTask;
}
