using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgentRp.Models;
using AgentRp.Services;
using Microsoft.Extensions.Logging;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class RoleplaySession(ILiveRoleplayStore liveStore, IModelCapabilityCatalog? capabilityCatalog = null, ITextGenerationService? textGenerationService = null, IMessageSpeechService? messageSpeechService = null, SceneTransitionService? sceneTransitionService = null, IStoryAssistantService? storyAssistantService = null, IStoryCardCatalogService? storyCardCatalog = null, IAiProviderCapabilityPipeline? capabilityPipeline = null, IAiProviderWidgetService? providerWidgetService = null, IEntityNotifier? entityNotifier = null, IGlobalModelSelectionStore? globalModelSelectionStore = null, IGlobalPromptLibraryStore? globalPromptLibraryStore = null, IGlobalModelTuningStore? globalModelTuningStore = null, IModelSelectionNotifier? modelSelectionNotifier = null, ICurrentAppUserAccessor? currentUserAccessor = null, ILoggerFactory? loggerFactory = null) : IAsyncDisposable
{
	private readonly Guid _sessionId = Guid.NewGuid();

	private readonly IAiProviderCapabilityPipeline _capabilityPipeline = capabilityPipeline ?? new AiProviderCapabilityPipeline(capabilityCatalog ?? NullModelCapabilityCatalog.Instance);

	private readonly IAiProviderWidgetService _providerWidgetService = providerWidgetService ?? NullAiProviderWidgetService.Instance;

	private readonly IEntityNotifier _entityNotifier = entityNotifier ?? NullEntityNotifier.Instance;

	private readonly IStoryCardCatalogService _storyCardCatalog = storyCardCatalog ?? NullStoryCardCatalogService.Instance;

	private readonly IGlobalModelSelectionStore _globalModelSelectionStore = globalModelSelectionStore ?? new GlobalModelSelectionStore(new InMemoryAppSettingsService());

	private readonly IGlobalPromptLibraryStore _globalPromptLibraryStore = globalPromptLibraryStore ?? new GlobalPromptLibraryStore(new InMemoryAppSettingsService());

	private readonly IGlobalModelTuningStore _globalModelTuningStore = globalModelTuningStore ?? new GlobalModelTuningStore(new InMemoryAppSettingsService());

	private readonly IModelSelectionNotifier _modelSelectionNotifier = modelSelectionNotifier ?? NullModelSelectionNotifier.Instance;

	private readonly ICurrentAppUserAccessor? _currentUserAccessor = currentUserAccessor;

	private bool _initialized;

	private string? _activeChatId;

	public ActiveChatContext ActiveChat { get; } = new ActiveChatContext();

	public ChatRegistry Registry { get; private set; } = null;

	public ChatListStore Chats { get; private set; } = null;

	public ProviderStore Providers { get; private set; } = null;

	public ModelSelectionStore ModelSelection { get; private set; } = null;

	public GlobalPromptLibrarySessionStore PromptLibrary { get; private set; } = null;

	public GlobalModelTuningSessionStore ModelTuning { get; private set; } = null;

	public ChatWorkspace Chat { get; private set; } = null;

	public bool IsInitialized => _initialized;

	public CurrentAppUser CurrentUser { get; private set; } = null;

	public RpCharacter? SpeakingAs { get; private set; }

	public event Func<Task>? Changed;

	public async Task InitializeAsync(bool selectFirstStory = true)
	{
		if (!_initialized)
		{
			CurrentAppUser currentUser = ((_currentUserAccessor != null) ? (await _currentUserAccessor.GetCurrentUserAsync()) : new CurrentAppUser(Guid.Empty, "dev.user@local", "DEV.USER@LOCAL", "Development User", new HashSet<string>(StringComparer.Ordinal) { "Admin", "User" }));
			CurrentUser = currentUser;
			Registry = new ChatRegistry(_sessionId, liveStore, ActiveChat, CurrentUser);
			Chats = new ChatListStore(_sessionId, liveStore, Registry, ActiveChat, CurrentUser);
			Chats.ActiveSession = this;
			Providers = new ProviderStore(_sessionId, liveStore, CurrentUser, _capabilityPipeline, _providerWidgetService, loggerFactory?.CreateLogger<ProviderStore>());
			ModelSelection = new ModelSelectionStore(Providers, ActiveChat, Registry, _globalModelSelectionStore, _modelSelectionNotifier);
			Providers.ModelSelection = ModelSelection;
			PromptLibrary = new GlobalPromptLibrarySessionStore(_globalPromptLibraryStore);
			ModelTuning = new GlobalModelTuningSessionStore(_globalModelTuningStore);
			Chat = new ChatWorkspace(ActiveChat, Registry, Providers, ModelSelection, PromptLibrary, ModelTuning, textGenerationService ?? NullTextGenerationService.Instance, sceneTransitionService ?? new SceneTransitionService(), messageSpeechService, storyAssistantService, _entityNotifier, _storyCardCatalog, CurrentUser, loggerFactory);
			liveStore.Changed += OnLiveStoreChanged;
			await ModelSelection.LoadAsync();
			await PromptLibrary.LoadAsync();
			await ModelTuning.LoadAsync();
			await Chats.LoadAsync();
			await Providers.LoadAsync();
			StoryPreview first = Chats.Items.FirstOrDefault();
			if (selectFirstStory && first != null)
			{
				await Chats.SelectAsync(first.ChatId);
			}
			_initialized = true;
		}
	}

	private async Task OnLiveStoreChanged(RoleplayStoreNotification notification)
	{
		if (!(notification.OriginSessionId == _sessionId))
		{
			if (notification.Area == RoleplayStoreArea.Chats)
			{
				await Chats.RefreshAsync();
			}
			else if (notification.Area == RoleplayStoreArea.Providers)
			{
				await Providers.RefreshAsync();
			}
			else if (notification.ChatId != null && !(notification.ChatId != _activeChatId))
			{
				await Registry.RefreshActiveAsync(notification.Area);
			}
		}
	}

	internal void SetActiveChatId(string? chatId)
	{
		if (!(_activeChatId == chatId))
		{
			liveStore.ReleaseChat(_sessionId, _activeChatId);
			_activeChatId = chatId;
		}
	}

	public async Task SetSpeakingAsAsync(RpCharacter? character)
	{
		SpeakingAs = character;
		Func<Task> changed = this.Changed;
		if (changed != null)
		{
			await changed();
		}
	}

	public async ValueTask DisposeAsync()
	{
		liveStore.Changed -= OnLiveStoreChanged;
		liveStore.ReleaseChat(_sessionId, _activeChatId);
		ModelSelection?.Dispose();
		await Task.CompletedTask;
	}
}
