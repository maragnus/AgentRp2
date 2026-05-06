using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public sealed class RoleplaySession(
    ILiveRoleplayStore liveStore,
    ITextGenerationService? textGenerationService = null,
    IStoryAssistantService? storyAssistantService = null) : IAsyncDisposable
{
    readonly Guid _sessionId = Guid.NewGuid();
    bool _initialized;
    string? _activeChatId;

    public ActiveChatContext ActiveChat { get; } = new();
    public ChatRegistry Registry { get; private set; } = null!;
    public ChatListStore Chats { get; private set; } = null!;
    public ProviderStore Providers { get; private set; } = null!;
    public ChatWorkspace Chat { get; private set; } = null!;

    public bool IsInitialized => _initialized;
    public RpCharacter? SpeakingAs { get; private set; }
    public event Func<Task>? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        Registry = new(_sessionId, liveStore, ActiveChat);
        Chats = new(_sessionId, liveStore, Registry, ActiveChat);
        Chats.ActiveSession = this;
        Providers = new(_sessionId, liveStore);
        Chat = new(ActiveChat, Registry, Providers, textGenerationService ?? NullTextGenerationService.Instance, storyAssistantService);
        liveStore.Changed += OnLiveStoreChanged;

        await Chats.LoadAsync();
        await Providers.LoadAsync();
        var first = Chats.Items.FirstOrDefault();
        if (first is not null)
            await Chats.SelectAsync(first.Id);

        _initialized = true;
    }

    async Task OnLiveStoreChanged(RoleplayStoreNotification notification)
    {
        if (notification.OriginSessionId == _sessionId)
            return;

        if (notification.Area == RoleplayStoreArea.Chats)
        {
            await Chats.RefreshAsync();
            return;
        }

        if (notification.Area == RoleplayStoreArea.Providers)
        {
            await Providers.RefreshAsync();
            return;
        }

        if (notification.ChatId is null || notification.ChatId != _activeChatId)
            return;

        await Registry.RefreshActiveAsync(notification.Area);
    }

    internal void SetActiveChatId(string? chatId)
    {
        if (_activeChatId == chatId)
            return;

        liveStore.ReleaseChat(_sessionId, _activeChatId);
        _activeChatId = chatId;
    }

    public async Task SetSpeakingAsAsync(RpCharacter? character)
    {
        SpeakingAs = character;
        var changed = Changed;
        if (changed is not null)
            await changed.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        liveStore.Changed -= OnLiveStoreChanged;
        liveStore.ReleaseChat(_sessionId, _activeChatId);
        await Task.CompletedTask;
    }
}

public sealed record ActiveChatChange(RpChatDocument? Document, RoleplayStoreArea? Area);

public sealed class ActiveChatContext
{
    public RpChatDocument? Current { get; private set; }
    public event Func<ActiveChatChange, Task>? Changed;

    public async Task SetAsync(RpChatDocument document)
    {
        Current = document;
        await NotifyAsync(null);
    }

    public async Task UpdateAsync(RpChatDocument document, RoleplayStoreArea area)
    {
        Current = document;
        await NotifyAsync(area);
    }

    async Task NotifyAsync(RoleplayStoreArea? area)
    {
        var changed = Changed;
        if (changed is not null)
            await changed.Invoke(new(Current, area));
    }
}

public sealed class ChatRegistry(Guid sessionId, ILiveRoleplayStore liveStore, ActiveChatContext activeChat)
{
    public async Task<RpChatDocument> OpenAsync(string chatId)
    {
        var document = await liveStore.OpenChatAsync(sessionId, chatId);
        await activeChat.SetAsync(document);
        return document;
    }

    public async Task ReplaceAreaAsync(RpChatDocument document, RoleplayStoreArea area)
    {
        await liveStore.ReplaceChatAreaAsync(sessionId, document.Chat.Id, document, area);
    }

    public async Task RefreshActiveAsync(RoleplayStoreArea area)
    {
        if (activeChat.Current is null)
            return;

        var snapshot = await liveStore.GetChatSnapshotAsync(activeChat.Current.Chat.Id);
        await activeChat.UpdateAsync(snapshot, area);
    }
}

public sealed class ChatWorkspace
{
    public ChatWorkspace(ActiveChatContext activeChat, ChatRegistry registry, ProviderStore providers, ITextGenerationService textGenerationService, IStoryAssistantService? storyAssistantService)
    {
        Characters = new(activeChat, registry);
        Locations = new(activeChat, registry);
        Items = new(activeChat, registry);
        Timeline = new(activeChat, registry);
        Images = new(activeChat, registry);
        Transcript = new(activeChat, registry, providers, textGenerationService);
        StoryAssistant = new(activeChat, registry, providers, storyAssistantService);
        NarratorProfile = new(activeChat, registry);
        PromptLibrary = new(activeChat, registry);
        CharacterTraitLibrary = new(activeChat, registry);
        ModelTuning = new(activeChat, registry);

        Characters.Start();
        Locations.Start();
        Items.Start();
        Timeline.Start();
        Images.Start();
        Transcript.Start();
        StoryAssistant.Start();
        NarratorProfile.Start();
        PromptLibrary.Start();
        CharacterTraitLibrary.Start();
        ModelTuning.Start();
    }

    public CharacterStore Characters { get; }
    public LocationStore Locations { get; }
    public ItemStore Items { get; }
    public TimelineStore Timeline { get; }
    public ImageStore Images { get; }
    public TranscriptStore Transcript { get; }
    public StoryAssistantStore StoryAssistant { get; }
    public NarratorProfileStore NarratorProfile { get; }
    public PromptLibraryStore PromptLibrary { get; }
    public CharacterTraitLibraryStore CharacterTraitLibrary { get; }
    public ModelTuningStore ModelTuning { get; }
}

public sealed class ChatListStore(Guid sessionId, ILiveRoleplayStore liveStore, ChatRegistry registry, ActiveChatContext activeChat) : StoreBase
{
    readonly List<RpChat> _items = [];

    public IReadOnlyList<RpChat> Items => _items;
    public RpChat? Active => activeChat.Current?.Chat;

    public async Task LoadAsync() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        _items.Clear();
        _items.AddRange((await liveStore.LoadChatsAsync()).Select(SessionCloner.Clone));
        await NotifyChangedAsync();
    }

    public async Task SelectAsync(string chatId)
    {
        var document = await registry.OpenAsync(chatId);
        await RefreshAsync();
        ActiveSession?.SetActiveChatId(document.Chat.Id);
    }

    public RoleplaySession? ActiveSession { get; set; }

    public async Task<RpChat> AddAsync(string location)
    {
        var chats = await liveStore.AddChatAsync(sessionId, location, activeChat.Current);
        _items.Clear();
        _items.AddRange(chats.Select(SessionCloner.Clone));
        var chat = _items.First();
        await SelectAsync(chat.Id);
        await NotifyChangedAsync();
        return chat;
    }
}

public sealed class ProviderStore(Guid sessionId, ILiveRoleplayStore liveStore) : StoreBase
{
    readonly List<AiProvider> _items = [];

    public IReadOnlyList<AiProvider> Items => _items;

    public async Task LoadAsync() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        _items.Clear();
        _items.AddRange((await liveStore.LoadProvidersAsync()).Select(SessionCloner.Clone));
        await NotifyChangedAsync();
    }

    public async Task AddAsync(AiProvider provider)
    {
        _items.Add(provider);
        await MarkChangedAsync();
    }

    public async Task DeleteAsync(string id)
    {
        _items.RemoveAll(provider => provider.Id == id);
        await MarkChangedAsync();
    }

    public async Task SetModelsAsync(AiProvider provider, bool enabled)
    {
        foreach (var model in provider.Models)
        {
            if (enabled)
                AiProviderModelSelectionRules.SetChatSelected(model, true);
            else
                AiProviderModelSelectionRules.ClearSelectedRoles(model);
        }

        await MarkChangedAsync();
    }

    public async Task SetActiveTextModelAsync(string providerId, string modelId)
    {
        var targetProvider = _items.FirstOrDefault(provider => provider.Id == providerId)
            ?? throw new InvalidOperationException("Selecting the AI model failed because the provider is not available.");
        if (!targetProvider.Enabled)
            throw new InvalidOperationException($"Selecting the AI model failed because {targetProvider.Name} is disabled.");

        var targetModel = targetProvider.Models.FirstOrDefault(model => model.Id == modelId)
            ?? throw new InvalidOperationException("Selecting the AI model failed because the model is not available.");
        if (!AiProviderModelSelectionRules.IsSelectedForChat(targetModel))
            throw new InvalidOperationException($"Selecting the AI model failed because {DisplayName(targetModel)} is not enabled for chat.");

        foreach (var provider in _items)
        {
            foreach (var model in provider.Models)
                model.ActiveText = false;
        }

        targetModel.ActiveText = true;
        await MarkChangedAsync();
    }

    public async Task MarkChangedAsync()
    {
        NormalizeActiveTextSelection();
        await liveStore.ReplaceProvidersAsync(sessionId, _items);
        await NotifyChangedAsync();
    }

    void NormalizeActiveTextSelection()
    {
        var active = _items
            .Where(provider => provider.Enabled)
            .SelectMany(provider => provider.Models.Select(model => new { Provider = provider, Model = model }))
            .Where(item => item.Model.ActiveText && AiProviderModelSelectionRules.IsSelectedForChat(item.Model))
            .ToList();

        foreach (var item in _items.SelectMany(provider => provider.Models))
            item.ActiveText = false;

        if (active.Count > 0)
            active[0].Model.ActiveText = true;
    }

    static string DisplayName(AiProviderModel model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName;
}
