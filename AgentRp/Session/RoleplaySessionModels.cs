using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public sealed class RpChatDocument
{
    public RpChat Chat { get; set; } = new();
    public List<RpCharacter> Characters { get; set; } = [];
    public List<RpLocation> Locations { get; set; } = [];
    public List<RpItem> Items { get; set; } = [];
    public List<RpTimelineEntry> Timeline { get; set; } = [];
    public List<GalleryImage> Images { get; set; } = [];
    public RpTranscriptState Transcript { get; set; } = new();
    public StoryAssistantState StoryAssistant { get; set; } = new();
    public NarratorProfileState NarratorProfile { get; set; } = NarratorProfileState.CreateDefault();
    public PromptLibraryState PromptLibrary { get; set; } = PromptLibraryState.CreateDefault();
    public CharacterTraitLibraryState CharacterTraitLibrary { get; set; } = CharacterTraitLibraryState.CreateDefault();
    public ModelTuningState ModelTuning { get; set; } = ModelTuningState.CreateDefault();
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

    public static NarratorProfileState CreateDefault() => NarratorProfileService.CreateDefaultState();
}

public sealed class PromptLibraryState
{
    public Dictionary<string, PromptPairState> Prompts { get; set; } = [];
    public Dictionary<string, List<ShapePromptState>> TurnShapes { get; set; } = [];

    public static PromptLibraryState CreateDefault() => PromptLibraryService.CreateDefaultState();
}

public sealed class PromptPairState
{
    public string System { get; set; } = "";
    public string User { get; set; } = "";
}

public sealed class ShapePromptState
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class CharacterTraitLibraryState
{
    public int SchemaVersion { get; set; } = 1;
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
