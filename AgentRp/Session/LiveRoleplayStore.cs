using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public enum RoleplayStoreArea
{
    Chats,
    Providers,
    Characters,
    Locations,
    Items,
    Timeline,
    Images,
    Transcript,
    StoryAssistant,
    NarratorProfile,
    PromptLibrary,
    CharacterTraitLibrary,
    ModelTuning
}

public sealed record RoleplayStoreNotification(Guid OriginSessionId, string? ChatId, RoleplayStoreArea Area, long Version);

public interface ILiveRoleplayStore
{
    event Func<RoleplayStoreNotification, Task>? Changed;

    Task<IReadOnlyList<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default);
    Task<RpChatDocument> OpenChatAsync(Guid sessionId, string chatId, CancellationToken cancellationToken = default);
    void ReleaseChat(Guid sessionId, string? chatId);
    Task<RpChatDocument> GetChatSnapshotAsync(string chatId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RpChat>> AddChatAsync(Guid originSessionId, string location, RpChatDocument? template, CancellationToken cancellationToken = default);
    Task ReplaceProvidersAsync(Guid originSessionId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default);
    Task ReplaceChatAreaAsync(Guid originSessionId, string chatId, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default);
}

public sealed class LiveRoleplayStore : ILiveRoleplayStore, IAsyncDisposable
{
    sealed class LoadedChat
    {
        public RpChatDocument Document { get; set; } = new();
        public long Version { get; set; }
        public DateTimeOffset LastAccess { get; set; }
        public HashSet<Guid> Sessions { get; } = [];
    }

    readonly IRoleplayPersistence _persistence;
    readonly BackgroundSessionWorker _worker = new();
    readonly TimeSpan _inactiveChatTtl;
    readonly Timer _cleanupTimer;
    readonly object _gate = new();
    readonly Dictionary<string, LoadedChat> _loadedChats = [];
    List<RpChat>? _chats;
    List<AiProvider>? _providers;
    long _chatListVersion;
    long _providerVersion;
    bool _disposed;

    public LiveRoleplayStore(IRoleplayPersistence persistence)
        : this(persistence, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1))
    {
    }

    public LiveRoleplayStore(IRoleplayPersistence persistence, TimeSpan inactiveChatTtl, TimeSpan cleanupInterval)
    {
        _persistence = persistence;
        _inactiveChatTtl = inactiveChatTtl;
        _cleanupTimer = new(_ => CleanupExpiredChats(), null, cleanupInterval, cleanupInterval);
    }

    public event Func<RoleplayStoreNotification, Task>? Changed;

    public async Task<IReadOnlyList<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default)
    {
        List<RpChat>? snapshot;
        lock (_gate)
            snapshot = _chats?.Select(SessionCloner.Clone).ToList();

        if (snapshot is not null)
            return snapshot;

        var loaded = await _persistence.LoadChatsAsync(cancellationToken);
        lock (_gate)
        {
            _chats ??= loaded.Select(SessionCloner.Clone).ToList();
            return _chats.Select(SessionCloner.Clone).ToList();
        }
    }

    public async Task<IReadOnlyList<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default)
    {
        List<AiProvider>? snapshot;
        lock (_gate)
            snapshot = _providers?.Select(SessionCloner.Clone).ToList();

        if (snapshot is not null)
            return snapshot;

        var loaded = await _persistence.LoadProvidersAsync(cancellationToken);
        lock (_gate)
        {
            _providers ??= loaded.Select(SessionCloner.Clone).ToList();
            return _providers.Select(SessionCloner.Clone).ToList();
        }
    }

    public async Task<RpChatDocument> OpenChatAsync(Guid sessionId, string chatId, CancellationToken cancellationToken = default)
    {
        await LoadChatsAsync(cancellationToken);

        lock (_gate)
        {
            if (_loadedChats.TryGetValue(chatId, out var loaded))
            {
                loaded.Sessions.Add(sessionId);
                loaded.LastAccess = DateTimeOffset.UtcNow;
                return SessionCloner.Clone(loaded.Document);
            }
        }

        var document = await _persistence.LoadChatDocumentAsync(chatId, cancellationToken);
        lock (_gate)
        {
            if (!_loadedChats.TryGetValue(chatId, out var loaded))
            {
                loaded = new()
                {
                    Document = SessionCloner.Clone(document),
                    Version = 1,
                    LastAccess = DateTimeOffset.UtcNow
                };
                _loadedChats[chatId] = loaded;
            }

            loaded.Sessions.Add(sessionId);
            loaded.LastAccess = DateTimeOffset.UtcNow;
            return SessionCloner.Clone(loaded.Document);
        }
    }

    public void ReleaseChat(Guid sessionId, string? chatId)
    {
        if (chatId is null)
            return;

        lock (_gate)
        {
            if (_loadedChats.TryGetValue(chatId, out var loaded))
            {
                loaded.Sessions.Remove(sessionId);
                loaded.LastAccess = DateTimeOffset.UtcNow;
            }
        }
    }

    public async Task<RpChatDocument> GetChatSnapshotAsync(string chatId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_loadedChats.TryGetValue(chatId, out var loaded))
            {
                loaded.LastAccess = DateTimeOffset.UtcNow;
                return SessionCloner.Clone(loaded.Document);
            }
        }

        return await OpenChatAsync(Guid.Empty, chatId, cancellationToken);
    }

    public async Task<IReadOnlyList<RpChat>> AddChatAsync(Guid originSessionId, string location, RpChatDocument? template, CancellationToken cancellationToken = default)
    {
        await LoadChatsAsync(cancellationToken);
        RpChat chat;
        RpChatDocument document;
        long version;

        lock (_gate)
        {
            chat = new() { Id = NextChatId(), Title = "Untitled Scene", Updated = RelativeDateFormatter.FormatDate(DateTime.UtcNow), Location = location };
            _chats!.Insert(0, chat);
            _chatListVersion++;
            version = _chatListVersion;
            document = new()
            {
                Chat = SessionCloner.Clone(chat),
                Characters = template?.Characters.Select(SessionCloner.Clone).ToList() ?? [],
                Locations = template?.Locations.Select(SessionCloner.Clone).ToList() ?? [],
                Items = template?.Items.Select(SessionCloner.Clone).ToList() ?? [],
                Timeline = template?.Timeline.Select(SessionCloner.Clone).ToList() ?? [],
                Images = template?.Images.Select(SessionCloner.Clone).ToList() ?? [],
                Transcript = new(),
                StoryAssistant = new(),
                NarratorProfile = template is null ? NarratorProfileState.CreateDefault() : SessionCloner.Clone(template.NarratorProfile)
            };
            document.Transcript.RootScene.LocationName = location;
            document.Transcript.RootScene.LocationId = document.Locations.FirstOrDefault(item => item.Name == location)?.Id
                ?? document.Locations.FirstOrDefault(locationItem => locationItem.IsActive)?.Id
                ?? document.Locations.FirstOrDefault()?.Id
                ?? "";
            document.Transcript.RootScene.InSceneCharacterIds = document.Characters.Where(character => character.InScene).Select(character => character.Id).ToList();
            document.Transcript.RootScene.InSceneItemIds = document.Items.Where(item => item.InScene).Select(item => item.Id).ToList();
            TranscriptProjector.Apply(document);
            ChatPreviewProjector.Apply(chat, document);
            _loadedChats[chat.Id] = new()
            {
                Document = SessionCloner.Clone(document),
                Version = 1,
                LastAccess = DateTimeOffset.UtcNow,
                Sessions = { originSessionId }
            };
        }

        QueueSaveChats();
        QueueSaveDocument(document);
        await NotifyAsync(new(originSessionId, null, RoleplayStoreArea.Chats, version));
        return await LoadChatsAsync(cancellationToken);
    }

    public async Task ReplaceProvidersAsync(Guid originSessionId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default)
    {
        long version;
        lock (_gate)
        {
            _providers = providers.Select(SessionCloner.Clone).ToList();
            _providerVersion++;
            version = _providerVersion;
        }

        QueueSaveProviders();
        await NotifyAsync(new(originSessionId, null, RoleplayStoreArea.Providers, version));
    }

    public async Task ReplaceChatAreaAsync(Guid originSessionId, string chatId, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default)
    {
        long version;
        RpChatDocument snapshot;
        lock (_gate)
        {
            if (!_loadedChats.TryGetValue(chatId, out var loaded))
            {
                loaded = new()
                {
                    Document = SessionCloner.Clone(document),
                    LastAccess = DateTimeOffset.UtcNow
                };
                _loadedChats[chatId] = loaded;
            }

            ApplyArea(loaded.Document, document, area);
            TranscriptProjector.Apply(loaded.Document);
            loaded.Version++;
            loaded.LastAccess = DateTimeOffset.UtcNow;
            version = loaded.Version;
            snapshot = SessionCloner.Clone(loaded.Document);
            UpdateChatMetadata(snapshot);
        }

        QueueSaveDocument(snapshot);
        if (area is RoleplayStoreArea.Characters or RoleplayStoreArea.Locations or RoleplayStoreArea.Images or RoleplayStoreArea.Transcript)
        {
            QueueSaveChats();
            await NotifyAsync(new(originSessionId, null, RoleplayStoreArea.Chats, _chatListVersion));
        }

        await NotifyAsync(new(originSessionId, chatId, area, version));
    }

    public void CleanupExpiredChats(DateTimeOffset? now = null)
    {
        var cutoff = (now ?? DateTimeOffset.UtcNow) - _inactiveChatTtl;
        lock (_gate)
        {
            foreach (var pair in _loadedChats.Where(pair => pair.Value.Sessions.Count == 0 && pair.Value.LastAccess <= cutoff).ToList())
                _loadedChats.Remove(pair.Key);
        }
    }

    public bool IsChatLoaded(string chatId)
    {
        lock (_gate)
            return _loadedChats.ContainsKey(chatId);
    }

    async Task NotifyAsync(RoleplayStoreNotification notification)
    {
        var changed = Changed;
        if (changed is not null)
            await changed.Invoke(notification);
    }

    void ApplyArea(RpChatDocument target, RpChatDocument source, RoleplayStoreArea area)
    {
        switch (area)
        {
            case RoleplayStoreArea.Characters:
                target.Characters = source.Characters.Select(SessionCloner.Clone).ToList();
                break;
            case RoleplayStoreArea.Locations:
                target.Locations = source.Locations.Select(SessionCloner.Clone).ToList();
                target.Chat.Location = source.Chat.Location;
                break;
            case RoleplayStoreArea.Items:
                target.Items = source.Items.Select(SessionCloner.Clone).ToList();
                break;
            case RoleplayStoreArea.Timeline:
                target.Timeline = source.Timeline.Select(SessionCloner.Clone).ToList();
                break;
            case RoleplayStoreArea.Images:
                target.Images = source.Images.Select(SessionCloner.Clone).ToList();
                break;
            case RoleplayStoreArea.Transcript:
                target.Transcript = SessionCloner.Clone(source.Transcript);
                break;
            case RoleplayStoreArea.StoryAssistant:
                target.StoryAssistant = SessionCloner.Clone(source.StoryAssistant);
                break;
            case RoleplayStoreArea.NarratorProfile:
                target.NarratorProfile = SessionCloner.Clone(source.NarratorProfile);
                break;
            case RoleplayStoreArea.PromptLibrary:
                target.PromptLibrary = SessionCloner.Clone(source.PromptLibrary);
                break;
            case RoleplayStoreArea.CharacterTraitLibrary:
                target.CharacterTraitLibrary = SessionCloner.Clone(source.CharacterTraitLibrary);
                break;
            case RoleplayStoreArea.ModelTuning:
                target.ModelTuning = SessionCloner.Clone(source.ModelTuning);
                break;
        }
    }

    void UpdateChatMetadata(RpChatDocument document)
    {
        if (_chats is null)
            return;

        var chat = _chats.FirstOrDefault(chat => chat.Id == document.Chat.Id);
        if (chat is null)
            return;

        ChatPreviewProjector.Apply(chat, document);
        _chatListVersion++;
    }

    void QueueSaveChats()
    {
        List<RpChat>? snapshot;
        lock (_gate)
            snapshot = _chats?.Select(SessionCloner.Clone).ToList();

        if (snapshot is not null)
            _worker.Enqueue(token => _persistence.SaveChatsAsync(snapshot, token));
    }

    void QueueSaveProviders()
    {
        List<AiProvider>? snapshot;
        lock (_gate)
            snapshot = _providers?.Select(SessionCloner.Clone).ToList();

        if (snapshot is not null)
            _worker.Enqueue(token => _persistence.SaveProvidersAsync(snapshot, token));
    }

    void QueueSaveDocument(RpChatDocument document)
    {
        var snapshot = SessionCloner.Clone(document);
        _worker.Enqueue(token => _persistence.SaveChatDocumentAsync(snapshot, token));
    }

    string NextChatId()
    {
        var next = _chats!
            .Select(chat => chat.Id)
            .Where(id => id.Length > 2 && id.StartsWith("ch", StringComparison.OrdinalIgnoreCase) && int.TryParse(id[2..], out _))
            .Select(id => int.Parse(id[2..]))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"ch{next}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _cleanupTimer.DisposeAsync();
        await _worker.DisposeAsync();
    }
}
