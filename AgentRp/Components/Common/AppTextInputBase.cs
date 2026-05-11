using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AgentRp.Models;

namespace AgentRp.Components.Common;

public abstract class AppTextInputBase<TComponent> : ComponentBase, IAsyncDisposable
    where TComponent : AppTextInputBase<TComponent>
{
    ElementReference Element;
    DotNetObjectReference<TComponent>? DotNetReference;
    IJSObjectReference? TextUpdate;
    bool IsRegisteringTextUpdate;
    bool PendingTextUpdate = true;
    bool PendingInitialEmptySync = true;
    bool Disposed;
    string LastParameterValue = "";
    string LastReportedValue = "";
    bool LastReportedEmpty;

    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected ILogger<TComponent> Logger { get; set; } = default!;

    [Parameter] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public bool IsEmpty { get; set; }
    [Parameter] public EventCallback<bool> IsEmptyChanged { get; set; }
    [Parameter] public TextUpdateMode UpdateMode { get; set; }

    protected ElementReference InputElement
    {
        get => Element;
        set => Element = value;
    }

    protected override void OnParametersSet()
    {
        if (!string.Equals(LastParameterValue, Value, StringComparison.Ordinal))
        {
            LastParameterValue = Value;
            LastReportedValue = Value;
            PendingInitialEmptySync = true;
        }

        LastReportedEmpty = IsEmpty;
        PendingTextUpdate = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await SyncInitialEmptyStateAsync();
        await EnsureTextUpdateAsync();
    }

    protected Task HandleNativeChange(ChangeEventArgs args)
    {
        if (UpdateMode != TextUpdateMode.None)
            return Task.CompletedTask;

        return NotifyTextValueChanged(args.Value?.ToString() ?? "");
    }

    [JSInvokable]
    public async Task NotifyTextValueChanged(string value)
    {
        if (!string.Equals(LastReportedValue, value, StringComparison.Ordinal))
        {
            LastReportedValue = value;
            LastParameterValue = value;
            await ValueChanged.InvokeAsync(value);
        }

        await NotifyTextEmptyChanged(IsEmptyValue(value));
    }

    [JSInvokable]
    public async Task NotifyTextEmptyChanged(bool isEmpty)
    {
        if (LastReportedEmpty == isEmpty)
            return;

        LastReportedEmpty = isEmpty;
        await IsEmptyChanged.InvokeAsync(isEmpty);
    }

    async Task SyncInitialEmptyStateAsync()
    {
        if (!PendingInitialEmptySync || !IsEmptyChanged.HasDelegate || UpdateMode == TextUpdateMode.None)
            return;

        PendingInitialEmptySync = false;
        await NotifyTextEmptyChanged(IsEmptyValue(Value));
    }

    async Task EnsureTextUpdateAsync()
    {
        if (UpdateMode == TextUpdateMode.None)
        {
            await DisposeTextUpdateAsync();
            return;
        }

        if (TextUpdate is null)
            await RegisterTextUpdateAsync();
        else if (PendingTextUpdate)
            await UpdateTextUpdateAsync();
    }

    async Task RegisterTextUpdateAsync()
    {
        if (IsRegisteringTextUpdate || Disposed)
            return;

        IsRegisteringTextUpdate = true;
        try
        {
            DotNetReference ??= DotNetObjectReference.Create((TComponent)this);
            TextUpdate = await JS.InvokeAsync<IJSObjectReference>(
                "agentRp.textInputs.track",
                Element,
                DotNetReference,
                BuildTextUpdateOptions());
            PendingTextUpdate = false;
        }
        catch (InvalidOperationException exception) when (IsDetachedElementReference(exception))
        {
            Logger.LogDebug(exception, "Skipping text input tracking because the element reference is detached.");
        }
        catch (JSDisconnectedException exception)
        {
            Logger.LogDebug(exception, "Skipping text input tracking because the browser circuit is disconnected.");
        }
        finally
        {
            IsRegisteringTextUpdate = false;
        }
    }

    async Task UpdateTextUpdateAsync()
    {
        if (TextUpdate is null)
            return;

        try
        {
            await TextUpdate.InvokeVoidAsync("update", BuildTextUpdateOptions());
            PendingTextUpdate = false;
        }
        catch (InvalidOperationException exception) when (IsDetachedElementReference(exception))
        {
            Logger.LogDebug(exception, "Skipping text input tracking update because the element reference is detached.");
            await DisposeTextUpdateAsync();
        }
        catch (JSDisconnectedException exception)
        {
            Logger.LogDebug(exception, "Skipping text input tracking update because the browser circuit is disconnected.");
        }
    }

    object BuildTextUpdateOptions() => new
    {
        mode = UpdateMode.ToString(),
        value = Value,
        isEmpty = IsEmpty,
        emptyDebounceMilliseconds = 100,
        changeDebounceMilliseconds = 2000,
        liveDebounceMilliseconds = 500
    };

    async ValueTask DisposeTextUpdateAsync()
    {
        if (TextUpdate is null)
            return;

        var textUpdate = TextUpdate;
        TextUpdate = null;
        PendingTextUpdate = true;

        try
        {
            await textUpdate.InvokeVoidAsync("dispose");
            await textUpdate.DisposeAsync();
        }
        catch (JSDisconnectedException exception)
        {
            Logger.LogDebug(exception, "Skipping text input tracking disposal because the browser circuit is disconnected.");
        }
        catch (InvalidOperationException exception) when (IsDetachedElementReference(exception))
        {
            Logger.LogDebug(exception, "Skipping text input tracking disposal because the element reference is detached.");
        }
        finally
        {
            DotNetReference?.Dispose();
            DotNetReference = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Disposed = true;
        await DisposeTextUpdateAsync();
        DotNetReference?.Dispose();
        DotNetReference = null;
        GC.SuppressFinalize(this);
    }

    static bool IsDetachedElementReference(InvalidOperationException exception) =>
        exception.Message.Contains("No element is currently associated", StringComparison.OrdinalIgnoreCase);

    static bool IsEmptyValue(string value) => string.IsNullOrWhiteSpace(value);
}
