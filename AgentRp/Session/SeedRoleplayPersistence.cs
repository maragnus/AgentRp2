using AgentRp.Models;

namespace AgentRp.Session;

public sealed class SeedRoleplayPersistence : IRoleplayPersistence
{
    readonly object _gate = new();
    List<RpChat> _chats = SeedData.Chats().Select(SessionCloner.Clone).ToList();
    List<AiProvider> _providers = SeedData.Providers().Select(SessionCloner.Clone).ToList();
    readonly Dictionary<string, RpChatDocument> _documents = [];

    public Task<List<StoryPreview>> LoadStoryPreviewsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_chats
                .Select(chat => StoryPreviewProjector.FromDocument(GetOrCreateDocument(chat.Id)))
                .ToList());
        }
    }

    public Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_providers.Select(SessionCloner.Clone).ToList());
    }

    public Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var document = SessionCloner.Clone(GetOrCreateDocument(chatId));
            TranscriptProjector.Apply(document);
            return Task.FromResult(document);
        }
    }

    public Task<RpTranscriptState> LoadActiveTranscriptAsync(string chatId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(SessionCloner.Clone(GetOrCreateDocument(chatId).Transcript));
    }

    public Task SaveStoryPreviewsAsync(IReadOnlyList<StoryPreview> previews, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _chats = previews.Select(ToChat).ToList();

        return Task.CompletedTask;
    }

    public Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _providers = providers.Select(SessionCloner.Clone).ToList();

        return Task.CompletedTask;
    }

    public Task CreateChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default) =>
        SaveChatDocumentAsync(document, cancellationToken);

    public Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _documents[document.Chat.Id] = SessionCloner.Clone(document);
            var chat = _chats.FirstOrDefault(chat => chat.Id == document.Chat.Id);
            if (chat is not null)
            {
                TranscriptProjector.Apply(document);
                var preview = StoryPreviewProjector.FromDocument(document);
                var updated = ToChat(preview);
                chat.Title = updated.Title;
                chat.Updated = updated.Updated;
                chat.LastMessageUtc = updated.LastMessageUtc;
                chat.LastGeneratedTurnNumber = updated.LastGeneratedTurnNumber;
                chat.Starred = updated.Starred;
                chat.Messages = updated.Messages;
                chat.Location = updated.Location;
                chat.ActiveLocation = updated.ActiveLocation;
                chat.SceneCharacters = updated.SceneCharacters;
            }
        }

        return Task.CompletedTask;
    }

    public Task SaveChatAreaAsync(RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default) =>
        SaveChatDocumentAsync(document, cancellationToken);

    RpChatDocument GetOrCreateDocument(string chatId)
    {
        if (_documents.TryGetValue(chatId, out var document))
            return document;

        var chat = _chats.FirstOrDefault(chat => chat.Id == chatId) ?? _chats.First();
        document = new()
        {
            Chat = SessionCloner.Clone(chat),
            Characters = SeedData.Characters().Select(SessionCloner.Clone).ToList(),
            CharacterRelationships = SeedData.CharacterRelationships().Select(SessionCloner.Clone).ToList(),
            Locations = SeedData.Locations().Select(SessionCloner.Clone).ToList(),
            Items = SeedData.Items().Select(SessionCloner.Clone).ToList(),
            Timeline = SeedData.Timeline().Select(SessionCloner.Clone).ToList(),
            Images = SeedData.GalleryImages().Select(SessionCloner.Clone).ToList(),
            Transcript = SessionCloner.Clone(SeedData.Transcript()),
            StoryAssistant = new(),
            ChatDirection = ChatDirectionState.CreateDefault(),
            NarratorProfile = NarratorProfileState.CreateDefault()
        };
        TranscriptProjector.Apply(document);
        _documents[chatId] = document;
        return document;
    }

    static StoryPreview ToPreview(RpChat chat) => new()
    {
        ChatId = chat.Id,
        Title = chat.Title,
        Starred = chat.Starred,
        VisibleTurnCount = chat.Messages,
        LastGeneratedTurnNumber = chat.LastGeneratedTurnNumber,
        LastMessageUtc = chat.LastMessageUtc,
        Updated = chat.Updated,
        ActiveLocation = chat.ActiveLocation is null && string.IsNullOrWhiteSpace(chat.Location)
            ? null
            : new()
            {
                LocationId = chat.ActiveLocation?.Id ?? "",
                Name = chat.ActiveLocation?.Name ?? chat.Location,
                Avatar = ToAvatar(chat.ActiveLocation?.Image)
            },
        SceneCharacters = chat.SceneCharacters.Select(character => new StoryPreviewCharacter
        {
            CharacterId = character.Id,
            Name = character.Name,
            Avatar = ToAvatar(character.Image)
        }).ToList()
    };

    static RpChat ToChat(StoryPreview preview) => new()
    {
        Id = preview.ChatId,
        Title = preview.Title,
        Starred = preview.Starred,
        Messages = preview.VisibleTurnCount,
        LastGeneratedTurnNumber = preview.LastGeneratedTurnNumber,
        LastMessageUtc = preview.LastMessageUtc,
        Updated = preview.Updated,
        Location = preview.ActiveLocation?.Name ?? "",
        ActiveLocation = preview.ActiveLocation is null
            ? null
            : new()
            {
                Id = preview.ActiveLocation.LocationId,
                Name = preview.ActiveLocation.Name,
                ImageId = preview.ActiveLocation.Avatar?.ImageId ?? "",
                Image = ToGalleryImage(preview.ActiveLocation.Avatar)
            },
        SceneCharacters = preview.SceneCharacters.Select(character => new RpChatSceneCharacter
        {
            Id = character.CharacterId,
            Name = character.Name,
            ImageId = character.Avatar?.ImageId ?? "",
            Image = ToGalleryImage(character.Avatar)
        }).ToList()
    };

    static StoryPreviewAvatar? ToAvatar(GalleryImage? image) =>
        image is null
            ? null
            : new()
            {
                ImageId = image.Id,
                Url = image.Url,
                FocusXPercent = image.AvatarFocusXPercent,
                FocusYPercent = image.AvatarFocusYPercent,
                ZoomPercent = image.AvatarZoomPercent
            };

    static GalleryImage? ToGalleryImage(StoryPreviewAvatar? avatar) =>
        avatar is null
            ? null
            : new()
            {
                Id = avatar.ImageId,
                Url = avatar.Url,
                AvatarFocusXPercent = avatar.FocusXPercent,
                AvatarFocusYPercent = avatar.FocusYPercent,
                AvatarZoomPercent = avatar.ZoomPercent
            };
}
