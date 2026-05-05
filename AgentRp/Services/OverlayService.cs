using Microsoft.AspNetCore.Components;

namespace AgentRp.Services;

public sealed class OverlayService
{
    readonly Dictionary<Guid, OverlayEntry> entries = [];

    public event Action? Changed;

    public IReadOnlyList<OverlayEntry> Entries => entries.Values
        .OrderBy(entry => entry.Layer)
        .ThenBy(entry => entry.OpenedAt)
        .ToList();

    public async Task ShowAsync(OverlayEntry entry)
    {
        foreach (var existing in entries.Values.Where(existing => existing.Id != entry.Id).ToList())
            await CloseAsync(existing.Id);

        entries[entry.Id] = entry;
        Changed?.Invoke();
    }

    public async Task CloseAsync(Guid id, bool notifyOwner = true)
    {
        if (!entries.Remove(id, out var entry))
            return;

        if (notifyOwner)
            await entry.CloseOwnerAsync();

        Changed?.Invoke();
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
    Func<Task> CloseOwnerAsync)
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
