using Microsoft.AspNetCore.Components;

namespace AgentRp.Services;

public sealed class OverlayService(ILogger<OverlayService> logger)
{
    readonly Dictionary<Guid, OverlayEntry> entries = [];

    public event Action? Changed;

    public IReadOnlyList<OverlayEntry> Entries => entries.Values
        .OrderBy(entry => entry.Layer)
        .ThenBy(entry => entry.OpenedAt)
        .ToList();

    public async Task ShowAsync(OverlayEntry entry)
    {
        logger.LogInformation(
            "Overlay show requested. Id={OverlayId} Owner={Owner} Class={Class} Layer={Layer} Placement={Placement} ExistingCount={ExistingCount}",
            entry.Id,
            entry.OwnerDebugName,
            entry.Class,
            entry.Layer,
            entry.Placement,
            entries.Count);

        foreach (var existing in entries.Values.Where(existing => existing.Id != entry.Id).ToList())
            await CloseAsync(existing.Id);

        entries[entry.Id] = entry;
        Changed?.Invoke();
    }

    public async Task CloseAsync(Guid id, bool notifyOwner = true)
    {
        if (!entries.Remove(id, out var entry))
            return;

        logger.LogInformation(
            "Overlay close requested. Id={OverlayId} Owner={Owner} Class={Class} Layer={Layer} NotifyOwner={NotifyOwner}",
            entry.Id,
            entry.OwnerDebugName,
            entry.Class,
            entry.Layer,
            notifyOwner);

        if (notifyOwner)
            await entry.CloseOwnerAsync();

        Changed?.Invoke();
    }

    public async Task CloseAllAsync(string reason)
    {
        if (entries.Count == 0)
            return;

        logger.LogInformation("Closing all overlays. Reason={Reason} Count={Count}", reason, entries.Count);
        foreach (var id in entries.Keys.ToList())
            await CloseAsync(id);
    }
}

public sealed record OverlayEntry(
    Guid Id,
    ElementReference Anchor,
    RenderFragment Content,
    OverlayPlacement Placement,
    OverlayLayer Layer,
    string? Class,
    bool CloseOnContentClick,
    Func<Task> CloseOwnerAsync,
    string OwnerDebugName)
{
    public DateTimeOffset OpenedAt { get; } = DateTimeOffset.UtcNow;
}

public enum OverlayPlacement
{
    BottomStart,
    BottomCenter,
    BottomEnd,
    TopStart,
    TopCenter,
    TopEnd
}

public enum OverlayLayer
{
    Page,
    Modal
}
