using AgentRp.Models;

namespace AgentRp.Session;

public sealed class SeedRoleplayPersistence : IRoleplayPersistence
{
    readonly object _gate = new();
    List<RpChat> _chats = SeedData.Chats().Select(SessionCloner.Clone).ToList();
    List<AiProvider> _providers = SeedData.Providers().Select(SessionCloner.Clone).ToList();
    readonly Dictionary<string, RpChatDocument> _documents = [];

    public Task<List<RpChat>> LoadChatsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_chats.Select(SessionCloner.Clone).ToList());
    }

    public Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_providers.Select(SessionCloner.Clone).ToList());
    }

    public Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(SessionCloner.Clone(GetOrCreateDocument(chatId)).ApplyProjection());
    }

    public Task SaveChatsAsync(IReadOnlyList<RpChat> chats, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _chats = chats.Select(SessionCloner.Clone).ToList();

        return Task.CompletedTask;
    }

    public Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _providers = providers.Select(SessionCloner.Clone).ToList();

        return Task.CompletedTask;
    }

    public Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _documents[document.Chat.Id] = SessionCloner.Clone(document);
            var chat = _chats.FirstOrDefault(chat => chat.Id == document.Chat.Id);
            if (chat is not null)
            {
                TranscriptProjector.Apply(document);
                chat.Title = document.Chat.Title;
                chat.Location = document.Chat.Location;
                chat.Messages = document.Chat.Messages;
                chat.Updated = document.Chat.Updated;
                chat.Starred = document.Chat.Starred;
            }
        }

        return Task.CompletedTask;
    }

    RpChatDocument GetOrCreateDocument(string chatId)
    {
        if (_documents.TryGetValue(chatId, out var document))
            return document;

        var chat = _chats.FirstOrDefault(chat => chat.Id == chatId) ?? _chats.First();
        document = new()
        {
            Chat = SessionCloner.Clone(chat),
            Characters = SeedData.Characters().Select(SessionCloner.Clone).ToList(),
            Locations = SeedData.Locations().Select(SessionCloner.Clone).ToList(),
            Items = SeedData.Items().Select(SessionCloner.Clone).ToList(),
            Timeline = SeedData.Timeline().Select(SessionCloner.Clone).ToList(),
            Images = SeedData.GalleryImages().Select(SessionCloner.Clone).ToList(),
            Transcript = SessionCloner.Clone(SeedData.Transcript()),
            StoryAssistant = new(),
            NarratorProfile = NarratorProfileState.CreateDefault()
        };
        TranscriptProjector.Apply(document);
        _documents[chatId] = document;
        return document;
    }
}
