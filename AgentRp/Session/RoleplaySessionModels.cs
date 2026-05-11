using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public sealed class RpChatDocument
{
    public RpChat Chat { get; set; } = new();
    public List<RpCharacter> Characters { get; set; } = [];
    public List<RpCharacterRelationship> CharacterRelationships { get; set; } = [];
    public List<RpLocation> Locations { get; set; } = [];
    public List<RpItem> Items { get; set; } = [];
    public List<RpTimelineEntry> Timeline { get; set; } = [];
    public List<GalleryImage> Images { get; set; } = [];
    public List<StoryCardInstance> StoryCards { get; set; } = [];
    public RpTranscriptState Transcript { get; set; } = new();
    public StoryAssistantState StoryAssistant { get; set; } = new();
    public ChatDirectionState ChatDirection { get; set; } = ChatDirectionState.CreateDefault();
    public NarratorProfileState NarratorProfile { get; set; } = NarratorProfileState.CreateDefault();
    public CharacterTraitLibraryState CharacterTraitLibrary { get; set; } = CharacterTraitLibraryState.CreateDefault();
    public ActiveModelSelectionsState ModelSelections { get; set; } = ActiveModelSelectionsState.CreateDefault();
}

public sealed class StoryCreationOptions
{
    public bool CopyCharacters { get; set; }
    public bool CopyLocations { get; set; }
    public bool CopyItems { get; set; }
    public bool CopyTimeline { get; set; }
    public bool CopyImages { get; set; }
    public bool CopyStoryDirection { get; set; }
    public bool CopyNarratorProfile { get; set; }
    public bool EnableTts { get; set; }
    public bool AutoSpeakNewMessages { get; set; }
    public Dictionary<string, CharacterVoiceSelection> NarratorVoiceSelections { get; set; } = [];
    public List<string> StoryCardTemplateIds { get; set; } = [];
    public ActiveModelSelectionsState ModelSelections { get; set; } = ActiveModelSelectionsState.CreateDefault();

    public static StoryCreationOptions Blank() => new();
}

public enum ContentIntensity
{
    Forbidden,
    Allowed,
    Encouraged
}

public sealed class ChatDirectionState
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime UpdatedUtc { get; set; }
    public List<string> Genres { get; set; } = [];
    public List<string> Tones { get; set; } = [];
    public List<string> Themes { get; set; } = [];
    public List<string> Pacing { get; set; } = [];
    public List<string> StoryFocus { get; set; } = [];
    public List<string> Boundaries { get; set; } = [];
    public ContentIntensity ExplicitContent { get; set; } = ContentIntensity.Allowed;
    public ContentIntensity ViolentContent { get; set; } = ContentIntensity.Allowed;
    public string Setting { get; set; } = "";
    public string Premise { get; set; } = "";
    public string CustomGuidance { get; set; } = "";

    public static ChatDirectionState CreateDefault() => ChatDirectionService.CreateDefaultState();
}

public sealed class ActiveModelSelectionsState
{
    public Dictionary<AiModelRole, ActiveModelSelectionState> Values { get; set; } = [];

    public static ActiveModelSelectionsState CreateDefault() => new();
}

public sealed class ActiveModelSelectionState
{
    public string ProviderId { get; set; } = "";
    public string ModelId { get; set; } = "";
}

public sealed class NarratorProfileState
{
    public int SchemaVersion { get; set; } = 1;
    public string VoicePreset { get; set; } = "cinematic-descriptive";
    public int SetupDepth { get; set; } = 1;
    public int VisualDetail { get; set; } = 1;
    public int TransitionContext { get; set; } = 1;
    public int Foreshadowing { get; set; }
    public int DirectionStrength { get; set; } = 1;
    public string CustomGuidance { get; set; } = "";
    public Dictionary<string, CharacterVoiceSelection> VoiceSelections { get; set; } = [];

    public static NarratorProfileState CreateDefault() => NarratorProfileService.CreateDefaultState();
}

public sealed class PromptLibraryState
{
    public Dictionary<string, PromptPairState> Prompts { get; set; } = [];
    public Dictionary<string, List<ShapePromptState>> TurnShapes { get; set; } = [];
    public Dictionary<string, PromptPairOverrideState> PromptOverrides { get; set; } = [];
    public Dictionary<string, List<ShapePromptOverrideState>> TurnShapeOverrides { get; set; } = [];

    public static PromptLibraryState CreateDefault() => PromptLibraryService.CreateOverrideState();
}

public sealed class PromptPairState
{
    public string System { get; set; } = "";
    public string User { get; set; } = "";
}

public sealed class PromptPairOverrideState
{
    public string? System { get; set; }
    public string? User { get; set; }
}

public sealed class ShapePromptState
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class ShapePromptOverrideState
{
    public string Id { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class CharacterTraitLibraryState
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime UpdatedUtc { get; set; }
    public List<CharacterOption> SceneRoles { get; set; } = [];
    public List<CharacterTraitGroupState> TraitCategories { get; set; } = [];
    public List<CharacterOption> CoreDrives { get; set; } = [];
    public List<CharacterOption> CoreFears { get; set; } = [];
    public List<CharacterOption> SurfaceMasks { get; set; } = [];
    public List<CharacterOption> HiddenTruths { get; set; } = [];
    public List<CharacterOption> SentenceStyles { get; set; } = [];
    public List<CharacterOption> HonestyStyles { get; set; } = [];
    public List<CharacterOption> EmotionalLeakages { get; set; } = [];
    public List<CharacterOption> ActionFingerprints { get; set; } = [];
    public List<CharacterOption> StressPatterns { get; set; } = [];
    public List<CharacterOption> SoftSpots { get; set; } = [];
    public List<CharacterOption> AvoidPatterns { get; set; } = [];
    public List<CharacterOption> HairColors { get; set; } = [];
    public List<CharacterOption> HairStyles { get; set; } = [];
    public List<CharacterOption> EyeColors { get; set; } = [];
    public List<CharacterOption> FaceShapes { get; set; } = [];
    public List<CharacterOption> SkinTones { get; set; } = [];
    public List<CharacterOption> Complexions { get; set; } = [];
    public List<CharacterOption> Heights { get; set; } = [];
    public List<CharacterOption> Builds { get; set; } = [];
    public List<CharacterOption> BodyProportions { get; set; } = [];
    public List<CharacterOption> Presentations { get; set; } = [];
    public List<CharacterOption> AttractivenessLevels { get; set; } = [];
    public List<string> BondTypes { get; set; } = [];
    public List<string> Dynamics { get; set; } = [];

    public static CharacterTraitLibraryState CreateDefault() => CharacterTraitLibraryService.CreateDefaultState();
}

public sealed class CharacterTraitGroupState
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public List<CharacterOption> Items { get; set; } = [];
}

public sealed class ModelTuningState
{
    public Dictionary<string, ModelTuningStepState> Values { get; set; } = [];

    public static ModelTuningState CreateDefault() => new()
    {
        Values = new()
        {
            ["snapshot"] = new(),
            ["sceneContinuity"] = new(),
            ["appearance"] = new(),
            ["selection"] = new(),
            ["planning"] = new(),
            ["prose"] = new()
        }
    };
}

public sealed class ModelTuningStepState
{
    public double? Temperature { get; set; }
    public string TopP { get; set; } = "";
    public string MaxTokens { get; set; } = "";
    public string Seed { get; set; } = "";
    public string FrequencyPenalty { get; set; } = "";
    public string PresencePenalty { get; set; } = "";
    public string StopSequences { get; set; } = "";
}
