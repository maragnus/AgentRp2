using AgentRp.Services;
using Microsoft.Extensions.Logging;

namespace AgentRp.Session;

public sealed class ChatWorkspace
{
	public CharacterStore Characters { get; }

	public LocationStore Locations { get; }

	public ItemStore Items { get; }

	public TimelineStore Timeline { get; }

	public ImageStore Images { get; }

	public TranscriptStore Transcript { get; }

	public StoryAssistantStore StoryAssistant { get; }

	public ChatDirectionStore ChatDirection { get; }

	public NarratorProfileStore NarratorProfile { get; }

	public CharacterTraitLibraryStore CharacterTraitLibrary { get; }

	public ChatWorkspace(ActiveChatContext activeChat, ChatRegistry registry, ProviderStore providers, ModelSelectionStore modelSelection, GlobalPromptLibrarySessionStore promptLibrary, GlobalModelTuningSessionStore modelTuning, ITextGenerationService textGenerationService, SceneTransitionService sceneTransitionService, IMessageSpeechService? messageSpeechService, IStoryAssistantService? storyAssistantService, IEntityNotifier entityNotifier, ILoggerFactory? loggerFactory = null)
	{
		Characters = new CharacterStore(activeChat, registry, entityNotifier);
		Locations = new LocationStore(activeChat, registry, entityNotifier);
		Items = new ItemStore(activeChat, registry, entityNotifier);
		Timeline = new TimelineStore(activeChat, registry);
		Images = new ImageStore(activeChat, registry, entityNotifier);
		Transcript = new TranscriptStore(activeChat, registry, providers, modelSelection, promptLibrary, modelTuning, textGenerationService, sceneTransitionService, messageSpeechService, loggerFactory?.CreateLogger<TranscriptStore>());
		StoryAssistant = new StoryAssistantStore(activeChat, registry, providers, modelSelection, promptLibrary, modelTuning, Transcript, storyAssistantService, loggerFactory?.CreateLogger<StoryAssistantStore>());
		ChatDirection = new ChatDirectionStore(activeChat, registry);
		NarratorProfile = new NarratorProfileStore(activeChat, registry, entityNotifier);
		CharacterTraitLibrary = new CharacterTraitLibraryStore(activeChat, registry);
		Characters.Start();
		Locations.Start();
		Items.Start();
		Timeline.Start();
		Images.Start();
		Transcript.Start();
		StoryAssistant.Start();
		ChatDirection.Start();
		NarratorProfile.Start();
		CharacterTraitLibrary.Start();
	}
}
