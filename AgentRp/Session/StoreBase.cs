namespace AgentRp.Session;

public abstract class StoreBase : IDisposable
{
    public event Func<Task>? Changed;
    public Exception? LastBackgroundError { get; private set; }

    protected Task NotifyChangedAsync()
    {
        var changed = Changed;
        return changed is null ? Task.CompletedTask : changed.Invoke();
    }

    protected void CaptureBackgroundError(Exception exception)
    {
        LastBackgroundError = exception;
    }

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    protected virtual void DisposeCore()
    {
    }
}

public abstract class ActiveChatStoreBase : StoreBase
{
    readonly ActiveChatContext _activeChat;

    protected ActiveChatStoreBase(ActiveChatContext activeChat, ChatRegistry registry)
    {
        _activeChat = activeChat;
        Registry = registry;
        Document = activeChat.Current;
    }

    protected RpChatDocument? Document { get; private set; }

    protected ActiveChatContext ActiveChat => _activeChat;
    protected ChatRegistry Registry { get; }
    protected abstract RoleplayStoreArea Area { get; }

    public void Start()
    {
        _activeChat.Changed += OnActiveChatChanged;
        Attach(Document);
    }

    async Task OnActiveChatChanged(ActiveChatChange change)
    {
        if (change.Area is not null && change.Area != Area)
            return;

        Detach(Document);
        Document = change.Document;
        Attach(Document);
        await NotifyChangedAsync();
    }

    protected async Task SaveActiveDocumentAsync()
    {
        if (Document is null)
            return;

        await Registry.ReplaceAreaAsync(Document, Area);
        await NotifyChangedAsync();
    }

    protected virtual void Attach(RpChatDocument? document)
    {
    }

    protected virtual void Detach(RpChatDocument? document)
    {
    }

    protected override void DisposeCore()
    {
        _activeChat.Changed -= OnActiveChatChanged;
        Detach(Document);
    }
}
