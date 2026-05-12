using Microsoft.AspNetCore.Components.Forms;

namespace AgentRp.Components.Common;

public sealed class StatefulFormContext<TDraft> : IStatefulFormContext, IDisposable
    where TDraft : class
{
    readonly Func<Task> save;
    readonly Func<Task> close;
    Func<bool>? canSave;
    Func<TDraft, TDraft, bool>? hasChanges;
    readonly Dictionary<string, Func<bool>> scopes = new(StringComparer.Ordinal);
    readonly Action requestRender;
    Func<Task>? pendingAction;
    bool allowSaveWithoutChanges;
    bool disposed;

    public StatefulFormContext(
        TDraft model,
        TDraft baseline,
        Func<Task> save,
        Func<Task> close,
        Action requestRender,
        Func<bool>? canSave = null,
        bool allowSaveWithoutChanges = false,
        Func<TDraft, TDraft, bool>? hasChanges = null)
    {
        Model = model;
        Baseline = StatefulFormSnapshot.Clone(baseline);
        EditContext = new EditContext(Model);
        this.save = save;
        this.close = close;
        this.requestRender = requestRender;
        this.canSave = canSave;
        this.allowSaveWithoutChanges = allowSaveWithoutChanges;
        this.hasChanges = hasChanges;
        EditContext.OnFieldChanged += OnFieldChanged;
    }

    public event Action? Changed;

    public TDraft Model { get; private set; }
    public TDraft Baseline { get; private set; }
    public EditContext EditContext { get; private set; }
    public bool ShowUnsavedChangesDialog { get; private set; }
    public bool HasChanges => hasChanges?.Invoke(Model, Baseline) ?? !StatefulFormSnapshot.Equivalent(Model, Baseline);
    public bool CanSave => (allowSaveWithoutChanges || HasChanges) && (canSave?.Invoke() ?? true);

    public void UpdateOptions(
        Func<bool>? canSave,
        bool allowSaveWithoutChanges,
        Func<TDraft, TDraft, bool>? hasChanges)
    {
        this.canSave = canSave;
        this.allowSaveWithoutChanges = allowSaveWithoutChanges;
        this.hasChanges = hasChanges;
    }

    public void UpdateModel(TDraft model, TDraft baseline)
    {
        if (ReferenceEquals(Model, model))
            return;

        EditContext.OnFieldChanged -= OnFieldChanged;
        Model = model;
        Baseline = StatefulFormSnapshot.Clone(baseline);
        EditContext = new EditContext(Model);
        EditContext.OnFieldChanged += OnFieldChanged;
        NotifyChanged();
    }

    public void ResetBaseline(TDraft? baseline = null)
    {
        Baseline = StatefulFormSnapshot.Clone(baseline ?? Model);
        NotifyChanged();
    }

    public bool IsFieldDirty(FieldIdentifier field)
    {
        var prefix = ReferenceEquals(field.Model, Model)
            ? ""
            : StatefulFormPathResolver.FindObjectPath(Model, field.Model);

        if (prefix is null)
            return EditContext.IsModified(field) && HasChanges;

        var path = string.IsNullOrWhiteSpace(prefix) ? field.FieldName : $"{prefix}.{field.FieldName}";
        return IsPathDirty(path);
    }

    public bool IsPathDirty(string path)
    {
        var current = StatefulFormPathResolver.GetValue(Model, path);
        var baseline = StatefulFormPathResolver.GetValue(Baseline, path);
        return !StatefulFormSnapshot.Equivalent(current, baseline);
    }

    public bool IsAnyPathDirty(params string[] paths) =>
        paths.Any(IsPathDirty);

    public bool IsScopeDirty(string id) =>
        scopes.TryGetValue(id, out var isDirty) && isDirty();

    public void NotifyChanged()
    {
        if (disposed)
            return;

        Changed?.Invoke();
        requestRender();
    }

    public async Task SaveAsync()
    {
        if (!CanSave)
            return;

        await save();
        ResetBaseline();
    }

    public Task RequestCloseAsync() => GuardAsync(close);

    public async Task GuardAsync(Func<Task> action)
    {
        if (!HasChanges)
        {
            await action();
            return;
        }

        pendingAction = action;
        ShowUnsavedChangesDialog = true;
        NotifyChanged();
    }

    public async Task ConfirmSaveAndContinueAsync()
    {
        if (!CanSave)
            return;

        var action = pendingAction;
        pendingAction = null;
        ShowUnsavedChangesDialog = false;
        await SaveAsync();
        if (action is not null)
            await action();
    }

    public async Task AbandonAndContinueAsync()
    {
        var action = pendingAction;
        pendingAction = null;
        ShowUnsavedChangesDialog = false;
        Baseline = StatefulFormSnapshot.Clone(Model);
        NotifyChanged();
        if (action is not null)
            await action();
    }

    public Task CancelPendingActionAsync()
    {
        pendingAction = null;
        ShowUnsavedChangesDialog = false;
        NotifyChanged();
        return Task.CompletedTask;
    }

    public IDisposable RegisterScope(string id, Func<bool> isDirty)
    {
        scopes[id] = isDirty;
        NotifyChanged();
        return new ScopeRegistration(this, id);
    }

    void OnFieldChanged(object? sender, FieldChangedEventArgs args) => NotifyChanged();

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        EditContext.OnFieldChanged -= OnFieldChanged;
    }

    sealed class ScopeRegistration(StatefulFormContext<TDraft> owner, string id) : IDisposable
    {
        public void Dispose()
        {
            owner.scopes.Remove(id);
            owner.NotifyChanged();
        }
    }
}
