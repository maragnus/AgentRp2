namespace AgentRp.Services;

public sealed class DialogHelper(ILogger<DialogHelper> logger)
{
    readonly Queue<DialogRequest> pending = [];
    DialogRequest? current;

    public event Action? Changed;

    public DialogRequest? Current => current;

    public Task DisplayErrorAsync(string title, string message, string? errorDetails = null) =>
        ShowAsync(new DialogOptions(
            title,
            message,
            DialogKind.Error,
            errorDetails,
            new DialogButtonOptions(PrimaryText: "OK", PrimaryIcon: "check"),
            "warning"));

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string? details = null,
        DialogButtonOptions? buttons = null)
    {
        var result = await ShowAsync(new DialogOptions(
            title,
            message,
            DialogKind.Confirm,
            details,
            buttons ?? new DialogButtonOptions(ShowCancel: true, PrimaryText: "OK", PrimaryIcon: "check"),
            "question"));

        return result == DialogResult.Primary;
    }

    public Task<DialogResult> ShowAsync(DialogOptions options)
    {
        var request = new DialogRequest(Guid.NewGuid(), Normalize(options));
        pending.Enqueue(request);
        logger.LogInformation("Dialog queued. Id={DialogId} Kind={Kind} Title={Title} PendingCount={PendingCount}", request.Id, request.Options.Kind, request.Options.Title, pending.Count);
        ShowNext();
        return request.Completion.Task;
    }

    public Task CloseCurrentAsync(DialogResult result)
    {
        if (current is null)
            return Task.CompletedTask;

        var closed = current;
        current = null;
        logger.LogInformation("Dialog closed. Id={DialogId} Kind={Kind} Result={Result}", closed.Id, closed.Options.Kind, result);
        closed.Completion.TrySetResult(result);
        Changed?.Invoke();
        ShowNext();
        return Task.CompletedTask;
    }

    void ShowNext()
    {
        if (current is not null || pending.Count == 0)
            return;

        current = pending.Dequeue();
        logger.LogInformation("Dialog opened. Id={DialogId} Kind={Kind} Title={Title}", current.Id, current.Options.Kind, current.Options.Title);
        Changed?.Invoke();
    }

    static DialogOptions Normalize(DialogOptions options)
    {
        var buttons = options.Buttons ?? options.Kind switch
        {
            DialogKind.Confirm => new DialogButtonOptions(ShowCancel: true, PrimaryText: "OK", PrimaryIcon: "check"),
            DialogKind.Error => new DialogButtonOptions(PrimaryText: "OK", PrimaryIcon: "check"),
            _ => new DialogButtonOptions(PrimaryText: "OK", PrimaryIcon: "check")
        };

        var icon = string.IsNullOrWhiteSpace(options.Icon)
            ? options.Kind switch
            {
                DialogKind.Error => "warning",
                DialogKind.Confirm => "question",
                _ => "circle-info"
            }
            : options.Icon;

        return options with { Buttons = buttons, Icon = icon };
    }
}

public sealed record DialogOptions(
    string Title,
    string Message,
    DialogKind Kind = DialogKind.Message,
    string? Details = null,
    DialogButtonOptions? Buttons = null,
    string? Icon = null,
    string? Subtitle = null);

public sealed record DialogButtonOptions(
    bool ShowCancel = false,
    string CancelText = "Cancel",
    string PrimaryText = "OK",
    string PrimaryVariant = "primary",
    string? PrimaryIcon = null);

public sealed class DialogRequest(Guid id, DialogOptions options)
{
    internal TaskCompletionSource<DialogResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Guid Id { get; } = id;
    public DialogOptions Options { get; } = options;
}

public enum DialogKind
{
    Message,
    Error,
    Confirm
}

public enum DialogResult
{
    Cancelled,
    Primary
}
