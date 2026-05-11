using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public interface IGlobalModelSelectionStore
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    ActiveModelSelectionsState Snapshot();
    ActiveModelSelection? Resolve(AiModelRole role, IReadOnlyList<AiProvider> providers);
    Task SetActiveModelAsync(AiModelRole role, string providerId, string modelId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default);
    Task EnsureValidAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default);
}

public sealed class GlobalModelSelectionStore(
    IAppSettingsService appSettings,
    IModelSelectionNotifier? notifier = null) : IGlobalModelSelectionStore
{
    public const string SettingsKey = "activeModelSelections";

    static readonly AiModelRole[] Roles = [AiModelRole.Chat, AiModelRole.Reasoning, AiModelRole.Image, AiModelRole.Voice];

    readonly IModelSelectionNotifier _notifier = notifier ?? NullModelSelectionNotifier.Instance;
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly object _stateLock = new();
    ActiveModelSelectionsState _state = ActiveModelSelectionsState.CreateDefault();
    bool _loaded;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
                return;

            var loaded = SessionCloner.Clone(await appSettings.GetAsync(SettingsKey, ActiveModelSelectionsState.CreateDefault(), cancellationToken));
            lock (_stateLock)
                _state = loaded;
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public ActiveModelSelectionsState Snapshot()
    {
        lock (_stateLock)
            return SessionCloner.Clone(_state);
    }

    public ActiveModelSelection? Resolve(AiModelRole role, IReadOnlyList<AiProvider> providers)
    {
        lock (_stateLock)
            return TextModelTuningCatalog.TryResolveActiveModel(providers, role, _state);
    }

    public async Task SetActiveModelAsync(
        AiModelRole role,
        string providerId,
        string modelId,
        IReadOnlyList<AiProvider> providers,
        CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
        var selection = ResolveExplicit(role, providerId, modelId, providers)
            ?? throw new InvalidOperationException($"Selecting the AI model failed because the model is not enabled for {AiProviderModelSelectionRules.Label(role)}.");

        await SaveSelectionAsync(role, selection.Provider.Id, selection.Model.Id, ModelSelectionChangeKinds.Selected, cancellationToken);
    }

    public async Task EnsureValidAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken);
        var notifications = new List<ModelSelectionChangeNotification>();
        var changed = false;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var role in Roles)
            {
                ActiveModelSelectionState? saved;
                ActiveModelSelection? resolved;
                lock (_stateLock)
                {
                    _state.Values.TryGetValue(role, out saved);
                    resolved = TextModelTuningCatalog.TryResolveActiveModel(providers, role, _state);
                }
                if (resolved is null)
                {
                    var removed = false;
                    lock (_stateLock)
                        removed = _state.Values.Remove(role);
                    if (removed)
                    {
                        changed = true;
                        notifications.Add(new(role, "", "", ModelSelectionChangeKinds.Cleared));
                    }

                    continue;
                }

                if (saved is not null
                    && string.Equals(saved.ProviderId, resolved.Provider.Id, StringComparison.Ordinal)
                    && string.Equals(saved.ModelId, resolved.Model.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                lock (_stateLock)
                {
                    _state.Values[role] = new()
                    {
                        ProviderId = resolved.Provider.Id,
                        ModelId = resolved.Model.Id
                    };
                }
                changed = true;
                notifications.Add(new(role, resolved.Provider.Id, resolved.Model.Id, ModelSelectionChangeKinds.Fallback));
            }

            if (changed)
                await appSettings.SaveAsync(SettingsKey, Snapshot(), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        foreach (var notification in notifications)
            await _notifier.PublishAsync(notification);
    }

    async Task SaveSelectionAsync(
        AiModelRole role,
        string providerId,
        string modelId,
        string changeKind,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_stateLock)
            {
                _state.Values[role] = new()
                {
                    ProviderId = providerId,
                    ModelId = modelId
                };
            }
            await appSettings.SaveAsync(SettingsKey, Snapshot(), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        await _notifier.PublishAsync(new(role, providerId, modelId, changeKind));
    }

    static ActiveModelSelection? ResolveExplicit(AiModelRole role, string providerId, string modelId, IReadOnlyList<AiProvider> providers)
    {
        var provider = providers.FirstOrDefault(provider => provider.Id == providerId);
        var model = provider?.Models.FirstOrDefault(model => model.Id == modelId);
        return provider?.Enabled == true && model is not null && AiProviderModelSelectionRules.IsSelectedForRole(model, role)
            ? new(provider, model, model.Capabilities, role)
            : null;
    }
}

public sealed class ModelSelectionStore : IDisposable
{
    readonly ProviderStore _providers;
    readonly ActiveChatContext _activeChat;
    readonly ChatRegistry _registry;
    readonly IGlobalModelSelectionStore _globalStore;
    readonly IModelSelectionNotifier _notifier;

    public ModelSelectionStore(
        ProviderStore providers,
        ActiveChatContext activeChat,
        ChatRegistry registry,
        IGlobalModelSelectionStore globalStore,
        IModelSelectionNotifier? notifier = null)
    {
        _providers = providers;
        _activeChat = activeChat;
        _registry = registry;
        _globalStore = globalStore;
        _notifier = notifier ?? NullModelSelectionNotifier.Instance;
        _notifier.Changed += OnModelSelectionChanged;
        _activeChat.Changed += OnActiveChatChanged;
    }

    public event Func<Task>? Changed;
    public event Func<ModelSelectionChangeNotification, Task>? SelectionChanged;

    public ActiveModelSelectionsState State => SessionCloner.Clone(_activeChat.Current?.ModelSelections ?? _globalStore.Snapshot());

    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        _globalStore.LoadAsync(cancellationToken);

    public ActiveModelSelection? Resolve(AiModelRole role)
    {
        var documentSelection = ResolveExplicit(role, _activeChat.Current?.ModelSelections, _providers.Items);
        return documentSelection
            ?? _globalStore.Resolve(role, _providers.Items)
            ?? TextModelTuningCatalog.TryResolveActiveModel(_providers.Items, role);
    }

    public async Task SetActiveModelAsync(AiModelRole role, string providerId, string modelId, CancellationToken cancellationToken = default)
    {
        var document = _activeChat.Current ?? throw new InvalidOperationException("Selecting a model failed because no story is active.");
        var selection = ResolveExplicit(role, providerId, modelId, _providers.Items)
            ?? throw new InvalidOperationException($"Selecting the AI model failed because the model is not enabled for {AiProviderModelSelectionRules.Label(role)}.");

        document.ModelSelections.Values[role] = new()
        {
            ProviderId = selection.Provider.Id,
            ModelId = selection.Model.Id
        };
        await _registry.ReplaceAreaAsync(document, RoleplayStoreArea.ModelSelections);
        await _notifier.PublishAsync(new(role, selection.Provider.Id, selection.Model.Id, ModelSelectionChangeKinds.Selected));
    }

    public async Task EnsureValidAsync(CancellationToken cancellationToken = default)
    {
        await _globalStore.EnsureValidAsync(_providers.Items, cancellationToken);
        var document = _activeChat.Current;
        if (document is null)
            return;

        var removed = new List<AiModelRole>();
        foreach (var pair in document.ModelSelections.Values.ToList())
        {
            if (ResolveExplicit(pair.Key, pair.Value.ProviderId, pair.Value.ModelId, _providers.Items) is not null)
                continue;

            document.ModelSelections.Values.Remove(pair.Key);
            removed.Add(pair.Key);
        }

        if (removed.Count == 0)
            return;

        await _registry.ReplaceAreaAsync(document, RoleplayStoreArea.ModelSelections);
        foreach (var role in removed)
            await _notifier.PublishAsync(new(role, "", "", ModelSelectionChangeKinds.Cleared));
    }

    async Task OnModelSelectionChanged(ModelSelectionChangeNotification notification)
    {
        var selectionChanged = SelectionChanged;
        if (selectionChanged is not null)
            await selectionChanged.Invoke(notification);

        var changed = Changed;
        if (changed is not null)
            await changed.Invoke();
    }

    async Task OnActiveChatChanged(ActiveChatChange change)
    {
        if (change.Area is not null && change.Area != RoleplayStoreArea.ModelSelections)
            return;

        var changed = Changed;
        if (changed is not null)
            await changed.Invoke();
    }

    static ActiveModelSelection? ResolveExplicit(AiModelRole role, ActiveModelSelectionsState? selections, IReadOnlyList<AiProvider> providers)
    {
        if (selections is null || !selections.Values.TryGetValue(role, out var saved))
            return null;

        return ResolveExplicit(role, saved.ProviderId, saved.ModelId, providers);
    }

    static ActiveModelSelection? ResolveExplicit(AiModelRole role, string providerId, string modelId, IReadOnlyList<AiProvider> providers)
    {
        var provider = providers.FirstOrDefault(provider => provider.Id == providerId);
        var model = provider?.Models.FirstOrDefault(model => model.Id == modelId);
        return provider?.Enabled == true && model is not null && AiProviderModelSelectionRules.IsSelectedForRole(model, role)
            ? new(provider, model, model.Capabilities, role)
            : null;
    }

    public void Dispose()
    {
        _notifier.Changed -= OnModelSelectionChanged;
        _activeChat.Changed -= OnActiveChatChanged;
    }
}
