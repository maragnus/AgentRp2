using Microsoft.AspNetCore.Components.Forms;

namespace AgentRp.Components.Common;

public interface IStatefulFormContext
{
    event Action? Changed;

    bool HasChanges { get; }
    bool CanSave { get; }
    bool ShowUnsavedChangesDialog { get; }

    bool IsFieldDirty(FieldIdentifier field);
    bool IsPathDirty(string path);
    bool IsAnyPathDirty(params string[] paths);
    bool IsScopeDirty(string id);
    void NotifyChanged();
    Task SaveAsync();
    Task RequestCloseAsync();
    Task GuardAsync(Func<Task> action);
    Task ConfirmSaveAndContinueAsync();
    Task AbandonAndContinueAsync();
    Task CancelPendingActionAsync();
    IDisposable RegisterScope(string id, Func<bool> isDirty);
}
