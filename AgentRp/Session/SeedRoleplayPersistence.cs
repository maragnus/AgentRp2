using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class SeedRoleplayPersistence : IRoleplayPersistence
{
	private readonly object _gate = new object();

	private List<RpChat> _chats = SeedData.Chats().Select(SessionCloner.Clone).ToList();

	private List<AiProvider> _providers = SeedData.Providers().Select(SessionCloner.Clone).ToList();

	private readonly Dictionary<string, RpChatDocument> _documents = new Dictionary<string, RpChatDocument>();

	private static CurrentAppUser TestUser { get; } = new CurrentAppUser(Guid.Empty, "dev.user@local", "DEV.USER@LOCAL", "Development User", new HashSet<string>(StringComparer.Ordinal) { "Admin", "User" });

	public Task<List<StoryPreview>> LoadStoryPreviewsAsync(CurrentAppUser user, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			return Task.FromResult(_chats.Select(chat => StoryPreviewProjector.FromDocument(GetOrCreateDocument(chat.Id))).ToList());
		}
	}

	public Task<List<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			return Task.FromResult(_providers.Select(SessionCloner.Clone).ToList());
		}
	}

	public Task<RpChatDocument> LoadChatDocumentAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			RpChatDocument rpChatDocument = SessionCloner.Clone(GetOrCreateDocument(chatId));
			TranscriptProjector.Apply(rpChatDocument);
			return Task.FromResult(rpChatDocument);
		}
	}

	public Task<RpChatDocument> LoadChatDocumentAsync(string chatId, CancellationToken cancellationToken = default(CancellationToken))
	{
		return LoadChatDocumentAsync(TestUser, chatId, cancellationToken);
	}

	public Task<RpTranscriptState> LoadActiveTranscriptAsync(string chatId, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			return Task.FromResult(SessionCloner.Clone(GetOrCreateDocument(chatId).Transcript));
		}
	}

	public Task SaveStoryPreviewsAsync(CurrentAppUser user, IReadOnlyList<StoryPreview> previews, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			_chats = previews.Select(ToChat).ToList();
		}
		return Task.CompletedTask;
	}

	public Task SaveProvidersAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			_providers = providers.Select(SessionCloner.Clone).ToList();
		}
		return Task.CompletedTask;
	}

	public Task CreateChatDocumentAsync(CurrentAppUser user, RpChatDocument document, CancellationToken cancellationToken = default(CancellationToken))
	{
		document.Chat.UserId = user.Id;
		return SaveChatDocumentAsync(document, cancellationToken);
	}

	public Task SaveChatDocumentAsync(RpChatDocument document, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			_documents[document.Chat.Id] = SessionCloner.Clone(document);
			var rpChat = _chats.FirstOrDefault(chat => chat.Id == document.Chat.Id);
			if (rpChat != null)
			{
				TranscriptProjector.Apply(document);
				StoryPreview preview = StoryPreviewProjector.FromDocument(document);
				RpChat rpChat2 = ToChat(preview);
				rpChat.Title = rpChat2.Title;
				rpChat.Updated = rpChat2.Updated;
				rpChat.LastMessageUtc = rpChat2.LastMessageUtc;
				rpChat.LastGeneratedTurnNumber = rpChat2.LastGeneratedTurnNumber;
				rpChat.Starred = rpChat2.Starred;
				rpChat.Messages = rpChat2.Messages;
				rpChat.Location = rpChat2.Location;
				rpChat.ActiveLocation = rpChat2.ActiveLocation;
				rpChat.SceneCharacters = rpChat2.SceneCharacters;
			}
		}
		return Task.CompletedTask;
	}

	public Task SaveChatAreaAsync(CurrentAppUser user, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default(CancellationToken))
	{
		return SaveChatDocumentAsync(document, cancellationToken);
	}

	private RpChatDocument GetOrCreateDocument(string chatId)
	{
		if (_documents.TryGetValue(chatId, out var value))
		{
			return value;
		}
		RpChat value2 = _chats.FirstOrDefault(chat => chat.Id == chatId) ?? _chats.First();
        RpChatDocument rpChatDocument = new RpChatDocument
        {
            Chat = SessionCloner.Clone(value2),
            Characters = SeedData.Characters().Select(SessionCloner.Clone).ToList(),
            CharacterRelationships = SeedData.CharacterRelationships().Select(SessionCloner.Clone).ToList(),
            Locations = SeedData.Locations().Select(SessionCloner.Clone).ToList(),
            Items = SeedData.Items().Select(SessionCloner.Clone).ToList(),
            Timeline = SeedData.Timeline().Select(SessionCloner.Clone).ToList(),
            Images = SeedData.GalleryImages().Select(SessionCloner.Clone).ToList(),
            Transcript = SessionCloner.Clone(SeedData.Transcript()),
            StoryAssistant = new StoryAssistantState(),
            ChatDirection = ChatDirectionState.CreateDefault(),
            NarratorProfile = NarratorProfileState.CreateDefault()
        };
        value = rpChatDocument;
		TranscriptProjector.Apply(value);
		_documents[chatId] = value;
		return value;
	}

	private static StoryPreview ToPreview(RpChat chat)
	{
		return new StoryPreview
		{
			ChatId = chat.Id,
			Title = chat.Title,
			Starred = chat.Starred,
			VisibleTurnCount = chat.Messages,
			LastGeneratedTurnNumber = chat.LastGeneratedTurnNumber,
			LastMessageUtc = chat.LastMessageUtc,
			Updated = chat.Updated,
			ActiveLocation = ((chat.ActiveLocation == null && string.IsNullOrWhiteSpace(chat.Location)) ? null : new StoryPreviewLocation
			{
				LocationId = (chat.ActiveLocation?.Id ?? ""),
				Name = (chat.ActiveLocation?.Name ?? chat.Location),
				Avatar = ToAvatar(chat.ActiveLocation?.Image)
			}),
			SceneCharacters = chat.SceneCharacters.Select(character => new StoryPreviewCharacter
			{
				CharacterId = character.Id,
				Name = character.Name,
				Avatar = ToAvatar(character.Image)
			}).ToList()
		};
	}

	private static RpChat ToChat(StoryPreview preview)
	{
		return new RpChat
		{
			Id = preview.ChatId,
			Title = preview.Title,
			Starred = preview.Starred,
			Messages = preview.VisibleTurnCount,
			LastGeneratedTurnNumber = preview.LastGeneratedTurnNumber,
			LastMessageUtc = preview.LastMessageUtc,
			Updated = preview.Updated,
			Location = (preview.ActiveLocation?.Name ?? ""),
			ActiveLocation = ((preview.ActiveLocation == null) ? null : new RpChatSceneLocation
			{
				Id = preview.ActiveLocation.LocationId,
				Name = preview.ActiveLocation.Name,
				ImageId = (preview.ActiveLocation.Avatar?.ImageId ?? ""),
				Image = ToGalleryImage(preview.ActiveLocation.Avatar)
			}),
			SceneCharacters = preview.SceneCharacters.Select(character => new RpChatSceneCharacter
			{
				Id = character.CharacterId,
				Name = character.Name,
				ImageId = (character.Avatar?.ImageId ?? ""),
				Image = ToGalleryImage(character.Avatar)
			}).ToList()
		};
	}

	private static StoryPreviewAvatar? ToAvatar(GalleryImage? image)
	{
		return (image == null) ? null : new StoryPreviewAvatar
		{
			ImageId = image.Id,
			Url = image.Url,
			FocusXPercent = image.AvatarFocusXPercent,
			FocusYPercent = image.AvatarFocusYPercent,
			ZoomPercent = image.AvatarZoomPercent
		};
	}

	private static GalleryImage? ToGalleryImage(StoryPreviewAvatar? avatar)
	{
		return (avatar == null) ? null : new GalleryImage
		{
			Id = avatar.ImageId,
			Url = avatar.Url,
			AvatarFocusXPercent = avatar.FocusXPercent,
			AvatarFocusYPercent = avatar.FocusYPercent,
			AvatarZoomPercent = avatar.ZoomPercent
		};
	}
}
