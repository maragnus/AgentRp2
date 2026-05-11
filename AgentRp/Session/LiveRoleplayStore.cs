using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class LiveRoleplayStore : ILiveRoleplayStore, IAsyncDisposable
{
	private sealed class LoadedChat
	{
		public RpChatDocument Document { get; set; } = new RpChatDocument();

		public long Version { get; set; }

		public DateTimeOffset LastAccess { get; set; }

		public HashSet<Guid> Sessions { get; } = new HashSet<Guid>();
	}

	private readonly IRoleplayPersistence _persistence;
	private readonly IStoryCardCatalogService? _storyCardCatalog;

	private readonly BackgroundSessionWorker _worker = new BackgroundSessionWorker();

	private readonly TimeSpan _inactiveChatTtl;

	private readonly Timer _cleanupTimer;

	private readonly object _gate = new object();

	private readonly Dictionary<string, LoadedChat> _loadedChats = new Dictionary<string, LoadedChat>();

	private readonly Dictionary<string, List<StoryPreview>> _storyPreviews = new Dictionary<string, List<StoryPreview>>();

	private List<AiProvider>? _providers;

	private long _chatListVersion;

	private long _providerVersion;

	private bool _disposed;

	private const string AdminStoryPreviewCacheKey = "admin";

	public event Func<RoleplayStoreNotification, Task>? Changed;

	public LiveRoleplayStore(IRoleplayPersistence persistence)
		: this(persistence, TimeSpan.FromMinutes(10L), TimeSpan.FromMinutes(1L))
	{
	}

	public LiveRoleplayStore(IRoleplayPersistence persistence, IStoryCardCatalogService storyCardCatalog)
		: this(persistence, storyCardCatalog, TimeSpan.FromMinutes(10L), TimeSpan.FromMinutes(1L))
	{
	}

	public LiveRoleplayStore(IRoleplayPersistence persistence, TimeSpan inactiveChatTtl, TimeSpan cleanupInterval)
		: this(persistence, null, inactiveChatTtl, cleanupInterval)
	{
	}

	public LiveRoleplayStore(IRoleplayPersistence persistence, IStoryCardCatalogService? storyCardCatalog, TimeSpan inactiveChatTtl, TimeSpan cleanupInterval)
	{
		_persistence = persistence;
		_storyCardCatalog = storyCardCatalog;
		_inactiveChatTtl = inactiveChatTtl;
		_cleanupTimer = new Timer(delegate
		{
			CleanupExpiredChats();
		}, null, cleanupInterval, cleanupInterval);
	}

	public async Task<IReadOnlyList<StoryPreview>> LoadStoryPreviewsAsync(CurrentAppUser user, CancellationToken cancellationToken = default(CancellationToken))
	{
		string cacheKey = StoryPreviewCacheKey(user);
		List<StoryPreview>? snapshot;
		lock (_gate)
		{
			snapshot = _storyPreviews.TryGetValue(cacheKey, out List<StoryPreview>? cached) ? [.. cached.Select(SessionCloner.Clone)] : null;
		}
		if (snapshot != null)
		{
			return snapshot;
		}
		List<StoryPreview> loaded = await _persistence.LoadStoryPreviewsAsync(user, cancellationToken);
		lock (_gate)
		{
			if (!_storyPreviews.TryGetValue(cacheKey, out List<StoryPreview>? value))
			{
                value = loaded.Select(SessionCloner.Clone).ToList();
                _storyPreviews[cacheKey] = value;
			}
			return value.Select(SessionCloner.Clone).ToList();
		}
	}

	public async Task<IReadOnlyList<AiProvider>> LoadProvidersAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		List<AiProvider>? snapshot;
		lock (_gate)
		{
			snapshot = _providers?.Select(SessionCloner.Clone).ToList();
		}
		if (snapshot != null)
		{
			return snapshot;
		}
		List<AiProvider>? loaded = await _persistence.LoadProvidersAsync(cancellationToken);
		lock (_gate)
		{
			_providers ??= loaded.Select(SessionCloner.Clone).ToList();
			return _providers.Select(SessionCloner.Clone).ToList();
		}
	}

	public async Task<RpChatDocument> OpenChatAsync(CurrentAppUser user, Guid sessionId, string chatId, CancellationToken cancellationToken = default(CancellationToken))
	{
		await LoadStoryPreviewsAsync(user, cancellationToken);
		lock (_gate)
		{
			if (_loadedChats.TryGetValue(chatId, out var loaded))
			{
				EnsureStoryAccess(user, loaded.Document);
				loaded.Sessions.Add(sessionId);
				loaded.LastAccess = DateTimeOffset.UtcNow;
				return SessionCloner.Clone(loaded.Document);
			}
		}
		RpChatDocument document = await _persistence.LoadChatDocumentAsync(user, chatId, cancellationToken);
		EnsureStoryAccess(user, document);
		lock (_gate)
		{
			if (!_loadedChats.TryGetValue(chatId, out var loaded2))
			{
				loaded2 = new LoadedChat
				{
					Document = SessionCloner.Clone(document),
					Version = 1L,
					LastAccess = DateTimeOffset.UtcNow
				};
				_loadedChats[chatId] = loaded2;
			}
			loaded2.Sessions.Add(sessionId);
			loaded2.LastAccess = DateTimeOffset.UtcNow;
			return SessionCloner.Clone(loaded2.Document);
		}
	}

	public void ReleaseChat(Guid sessionId, string? chatId)
	{
		if (chatId == null)
		{
			return;
		}
		lock (_gate)
		{
			if (_loadedChats.TryGetValue(chatId, out var value))
			{
				value.Sessions.Remove(sessionId);
				value.LastAccess = DateTimeOffset.UtcNow;
			}
		}
	}

	public async Task<RpChatDocument> GetChatSnapshotAsync(CurrentAppUser user, string chatId, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			if (_loadedChats.TryGetValue(chatId, out var loaded))
			{
				EnsureStoryAccess(user, loaded.Document);
				loaded.LastAccess = DateTimeOffset.UtcNow;
				return SessionCloner.Clone(loaded.Document);
			}
		}
		return await OpenChatAsync(user, Guid.Empty, chatId, cancellationToken);
	}

	public async Task<IReadOnlyList<StoryPreview>> AddChatAsync(CurrentAppUser user, Guid originSessionId, StoryCreationOptions options, RpChatDocument? template, CancellationToken cancellationToken = default(CancellationToken))
	{
		await LoadStoryPreviewsAsync(user, cancellationToken);
		string location = ((!options.CopyLocations) ? "" : (template?.Chat.Location ?? ""));
		string cacheKey = StoryPreviewCacheKey(user);
		RpChatDocument document;
		long version;
		lock (_gate)
		{
			RpChat chat = new RpChat
			{
				Id = NextChatId(),
				Title = "Untitled Story",
				Updated = RelativeDateFormatter.FormatDate(DateTime.UtcNow),
				Location = location,
				UserId = user.Id
			};
			document = new RpChatDocument
			{
				Chat = SessionCloner.Clone(chat),
				Characters = ((options.CopyCharacters && template != null) ? template.Characters.Select(SessionCloner.Clone).ToList() : new List<RpCharacter>()),
				CharacterRelationships = ((options.CopyCharacters && template != null) ? template.CharacterRelationships.Select(SessionCloner.Clone).ToList() : new List<RpCharacterRelationship>()),
				Locations = ((options.CopyLocations && template != null) ? template.Locations.Select(SessionCloner.Clone).ToList() : new List<RpLocation>()),
				Items = ((options.CopyItems && template != null) ? template.Items.Select(SessionCloner.Clone).ToList() : new List<RpItem>()),
				Timeline = ((options.CopyTimeline && template != null) ? template.Timeline.Select(SessionCloner.Clone).ToList() : new List<RpTimelineEntry>()),
				Images = ((options.CopyImages && template != null) ? template.Images.Select(SessionCloner.Clone).ToList() : new List<GalleryImage>()),
				Transcript = new RpTranscriptState(),
				StoryAssistant = new StoryAssistantState(),
				ChatDirection = ((options.CopyStoryDirection && template != null) ? SessionCloner.Clone(template.ChatDirection) : ChatDirectionState.CreateDefault()),
				NarratorProfile = ((options.CopyNarratorProfile && template != null) ? SessionCloner.Clone(template.NarratorProfile) : NarratorProfileState.CreateDefault()),
				CharacterTraitLibrary = ((options.CopyCharacters && template != null) ? SessionCloner.Clone(template.CharacterTraitLibrary) : CharacterTraitLibraryState.CreateDefault()),
				ModelSelections = SessionCloner.Clone(options.ModelSelections)
			};
			ApplyCreationTtsOptions(document, options);
			if (!options.CopyImages)
			{
				ClearImageReferences(document);
			}
			document.Transcript.RootScene.LocationName = location;
			document.Transcript.RootScene.LocationId = document.Locations.FirstOrDefault(item => item.Name == location)?.Id ?? document.Locations.FirstOrDefault(locationItem => locationItem.IsActive)?.Id ?? document.Locations.FirstOrDefault()?.Id ?? "";
			document.Transcript.RootScene.InSceneCharacterIds = (from character in document.Characters
				where character.InScene
				select character.Id).ToList();
			document.Transcript.RootScene.InSceneItemIds = (from item in document.Items
				where item.InScene
				select item.Id).ToList();
			TranscriptProjector.Apply(document);
			StoryPreview preview = StoryPreviewProjector.FromDocument(document);
			_storyPreviews[cacheKey].Insert(0, preview);
			if (user.IsAdmin)
			{
				AddPreviewToOwnerCaches(user.Id, preview);
			}
			else if (_storyPreviews.TryGetValue("admin", out var adminPreviews))
			{
				adminPreviews.Insert(0, SessionCloner.Clone(preview));
			}
			_chatListVersion++;
			version = _chatListVersion;
			_loadedChats[chat.Id] = new LoadedChat
			{
				Document = SessionCloner.Clone(document),
				Version = 1L,
				LastAccess = DateTimeOffset.UtcNow,
				Sessions = { originSessionId }
			};
		}
		if (options.StoryCardTemplateIds.Count > 0)
		{
			if (_storyCardCatalog is null)
				throw new InvalidOperationException("Creating a story with story cards failed because the story card catalog is not available.");

			var selectedTemplateIds = options.StoryCardTemplateIds.Take(2).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
			var storyCards = new List<StoryCardInstance>();
			foreach (var templateId in selectedTemplateIds)
				storyCards.Add(await _storyCardCatalog.CreateInstanceAsync(user, document.Chat.Id, templateId, document.Chat.LastGeneratedTurnNumber, injected: false, cancellationToken));

			lock (_gate)
			{
				document.StoryCards = storyCards;
				if (_loadedChats.TryGetValue(document.Chat.Id, out var loaded))
					loaded.Document.StoryCards = storyCards.Select(SessionCloner.Clone).ToList();
			}
		}
		QueueSaveStoryPreviews();
		QueueCreateDocument(user, document);
		await NotifyAsync(new RoleplayStoreNotification(originSessionId, null, RoleplayStoreArea.Chats, version));
		return await LoadStoryPreviewsAsync(user, cancellationToken);
	}

	private static void ApplyCreationTtsOptions(RpChatDocument document, StoryCreationOptions options)
	{
		if (!options.EnableTts)
		{
			return;
		}
		document.Transcript.Options.AutoSpeakNewMessages = options.AutoSpeakNewMessages;
		foreach (KeyValuePair<string, CharacterVoiceSelection> narratorVoiceSelection in options.NarratorVoiceSelections)
		{
			if (!string.IsNullOrWhiteSpace(narratorVoiceSelection.Key) && !string.IsNullOrWhiteSpace(narratorVoiceSelection.Value.VoiceId))
			{
				document.NarratorProfile.VoiceSelections[narratorVoiceSelection.Key] = CloneVoiceSelection(narratorVoiceSelection.Value);
			}
		}
	}

	private static CharacterVoiceSelection CloneVoiceSelection(CharacterVoiceSelection selection)
	{
		return new CharacterVoiceSelection
		{
			VoiceId = selection.VoiceId,
			VoiceName = selection.VoiceName,
			UpdatedUtc = selection.UpdatedUtc
		};
	}

	private static void ClearImageReferences(RpChatDocument document)
	{
		foreach (RpCharacter character in document.Characters)
		{
			character.ImageId = "";
		}
		foreach (RpLocation location in document.Locations)
		{
			location.ImageId = "";
		}
		foreach (RpItem item in document.Items)
		{
			item.ImageId = "";
		}
	}

	public async Task ReplaceProvidersAsync(CurrentAppUser user, Guid originSessionId, IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (!user.IsAdmin)
		{
			throw new UnauthorizedAccessException("Only admins can manage AI providers.");
		}
		long version;
		lock (_gate)
		{
			_providers = providers.Select(SessionCloner.Clone).ToList();
			_providerVersion++;
			version = _providerVersion;
		}
		QueueSaveProviders();
		await NotifyAsync(new RoleplayStoreNotification(originSessionId, null, RoleplayStoreArea.Providers, version));
	}

	public async Task ReplaceChatAreaAsync(CurrentAppUser user, Guid originSessionId, string chatId, RpChatDocument document, RoleplayStoreArea area, CancellationToken cancellationToken = default(CancellationToken))
	{
		EnsureStoryAccess(user, document);
		long version;
		RpChatDocument snapshot;
		lock (_gate)
		{
			if (!_loadedChats.TryGetValue(chatId, out var loaded))
			{
				loaded = new LoadedChat
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
			UpdateStoryPreview(snapshot);
		}
		QueueSaveArea(user, snapshot, area);
		bool flag;
		switch (area)
		{
		case RoleplayStoreArea.Characters:
		case RoleplayStoreArea.Locations:
		case RoleplayStoreArea.Images:
		case RoleplayStoreArea.Transcript:
		case RoleplayStoreArea.ChatDirection:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			QueueSaveStoryPreviews();
			await NotifyAsync(new RoleplayStoreNotification(originSessionId, null, RoleplayStoreArea.Chats, _chatListVersion));
		}
		await NotifyAsync(new RoleplayStoreNotification(originSessionId, chatId, area, version));
	}

	public void CleanupExpiredChats(DateTimeOffset? now = null)
	{
		DateTimeOffset cutoff = (now ?? DateTimeOffset.UtcNow) - _inactiveChatTtl;
		lock (_gate)
		{
			foreach (KeyValuePair<string, LoadedChat> item in _loadedChats.Where<KeyValuePair<string, LoadedChat>>(pair => pair.Value.Sessions.Count == 0 && pair.Value.LastAccess <= cutoff).ToList())
			{
				_loadedChats.Remove(item.Key);
			}
		}
	}

	public bool IsChatLoaded(string chatId)
	{
		lock (_gate)
		{
			return _loadedChats.ContainsKey(chatId);
		}
	}

	private async Task NotifyAsync(RoleplayStoreNotification notification)
	{
		var changed = this.Changed;
		if (changed != null)
		{
			await changed(notification);
		}
	}

	private void ApplyArea(RpChatDocument target, RpChatDocument source, RoleplayStoreArea area)
	{
		switch (area)
		{
		case RoleplayStoreArea.Characters:
			target.Characters = source.Characters.Select(SessionCloner.Clone).ToList();
			target.CharacterRelationships = source.CharacterRelationships.Select(SessionCloner.Clone).ToList();
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
		case RoleplayStoreArea.ChatDirection:
			target.ChatDirection = SessionCloner.Clone(source.ChatDirection);
			target.Chat.Title = source.Chat.Title;
			break;
		case RoleplayStoreArea.NarratorProfile:
			target.NarratorProfile = SessionCloner.Clone(source.NarratorProfile);
			break;
		case RoleplayStoreArea.CharacterTraitLibrary:
			target.CharacterTraitLibrary = SessionCloner.Clone(source.CharacterTraitLibrary);
			break;
		case RoleplayStoreArea.ModelSelections:
			target.ModelSelections = SessionCloner.Clone(source.ModelSelections);
			break;
		}
	}

	private void UpdateStoryPreview(RpChatDocument document)
	{
		StoryPreview value = StoryPreviewProjector.FromDocument(document);
		bool flag = false;
		foreach (List<StoryPreview> value2 in _storyPreviews.Values)
		{
			int num = value2.FindIndex(preview => preview.ChatId == document.Chat.Id);
			if (num >= 0)
			{
				value2[num] = SessionCloner.Clone(value);
				flag = true;
			}
		}
		if (flag)
		{
			_chatListVersion++;
		}
	}

	private void QueueSaveStoryPreviews()
	{
		Dictionary<string, List<StoryPreview>> dictionary;
		lock (_gate)
		{
			dictionary = _storyPreviews.ToDictionary<KeyValuePair<string, List<StoryPreview>>, string, List<StoryPreview>>(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value.Select(SessionCloner.Clone).ToList(), StringComparer.Ordinal);
		}
		foreach (KeyValuePair<string, List<StoryPreview>> pair in dictionary)
		{
			var user = UserFromCacheKey(pair.Key);
			if (user != null)
			{
				_worker.Enqueue(token => _persistence.SaveStoryPreviewsAsync(user, pair.Value, token));
			}
		}
	}

	private void QueueSaveProviders()
	{
		List<AiProvider>? snapshot;
		lock (_gate)
		{
			snapshot = _providers?.Select(SessionCloner.Clone).ToList();
		}
		if (snapshot != null)
		{
			_worker.Enqueue(token => _persistence.SaveProvidersAsync(snapshot, token));
		}
	}

	private void QueueCreateDocument(CurrentAppUser user, RpChatDocument document)
	{
		RpChatDocument snapshot = SessionCloner.Clone(document);
		_worker.Enqueue(token => _persistence.CreateChatDocumentAsync(user, snapshot, token));
	}

	private void QueueSaveArea(CurrentAppUser user, RpChatDocument document, RoleplayStoreArea area)
	{
		RpChatDocument snapshot = SessionCloner.Clone(document);
		_worker.Enqueue(token => _persistence.SaveChatAreaAsync(user, snapshot, area, token));
	}

	private static string NextChatId()
	{
		return $"ch{Guid.NewGuid():N}";
	}

	private static string StoryPreviewCacheKey(CurrentAppUser user)
	{
		return user.IsAdmin ? "admin" : $"user:{user.Id:N}";
	}

	private static CurrentAppUser? UserFromCacheKey(string cacheKey)
	{
		object? result2;
		if (cacheKey.StartsWith("user:", StringComparison.Ordinal))
		{
			int length = "user:".Length;
			if (Guid.TryParse(cacheKey.Substring(length, cacheKey.Length - length), out var result))
			{
				result2 = new CurrentAppUser(result, "", "", "", new HashSet<string>(StringComparer.Ordinal) { "User" });
				goto IL_0061;
			}
		}
		result2 = null;
		goto IL_0061;
		IL_0061:
		return (CurrentAppUser?)result2;
	}

	private void AddPreviewToOwnerCaches(Guid ownerId, StoryPreview preview)
	{
		if (_storyPreviews.TryGetValue($"user:{ownerId:N}", out var value))
		{
			value.Insert(0, SessionCloner.Clone(preview));
		}
	}

	private static void EnsureStoryAccess(CurrentAppUser user, RpChatDocument document)
	{
		if (document.Chat.UserId == Guid.Empty || user.IsAdmin || document.Chat.UserId == user.Id)
		{
			return;
		}
		throw new UnauthorizedAccessException("Opening story '" + document.Chat.Id + "' failed because it belongs to a different user.");
	}

	public async ValueTask DisposeAsync()
	{
		if (!_disposed)
		{
			_disposed = true;
			await _cleanupTimer.DisposeAsync();
			await _worker.DisposeAsync();
		}
	}
}
