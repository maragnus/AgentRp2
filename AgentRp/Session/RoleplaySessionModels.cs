using AgentRp.Models;

namespace AgentRp.Session;

public sealed class RpChatDocument
{
    public RpChat Chat { get; set; } = new();
    public List<RpCharacter> Characters { get; set; } = [];
    public List<RpLocation> Locations { get; set; } = [];
    public List<RpItem> Items { get; set; } = [];
    public List<RpTimelineEntry> Timeline { get; set; } = [];
    public List<GalleryImage> Images { get; set; } = [];
    public List<RpMessage> Messages { get; set; } = [];
    public PromptLibraryState PromptLibrary { get; set; } = PromptLibraryState.CreateDefault();
    public ModelTuningState ModelTuning { get; set; } = ModelTuningState.CreateDefault();
}

public sealed class PromptLibraryState
{
    public Dictionary<string, PromptPairState> Prompts { get; set; } = [];
    public Dictionary<string, List<ShapePromptState>> TurnShapes { get; set; } = [];

    public static PromptLibraryState CreateDefault() => new()
    {
        Prompts = new()
        {
            ["appearance"] = new() { System = "You are a precise scene-state tracker for a collaborative fiction tool.", User = "Update observable positions, posture, expression, clothing, and proximity from {last_turn}." },
            ["selection"] = new() { System = "Choose the next responder from the active scene.", User = "Scene: {scene}\nCharacters: {characters}\nRecent turns: {transcript}" },
            ["planning"] = new() { System = "Produce a structured dramatic plan before prose.", User = "Character: {responder}\nTurn shape: {turn_shape}\nContext: {context}" },
            ["prose"] = new() { System = "Write polished contemporary roleplay prose.", User = "Write the next turn using {plan} and current appearance state." }
        },
        TurnShapes = new()
        {
            ["selection"] = Shapes("Compact", "Brief", "Extended"),
            ["planning"] = Shapes("Compact", "Brief", "Extended", "Monologue"),
            ["prose"] = Shapes("Compact", "Brief", "Extended", "Monologue", "Silent")
        }
    };

    static List<ShapePromptState> Shapes(params string[] labels) =>
        labels.Select(label => new ShapePromptState { Id = label.ToLowerInvariant().Replace(" ", "-"), Label = label, Value = $"Use {label.ToLowerInvariant()} pacing and scope." }).ToList();
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

public sealed class ModelTuningState
{
    public Dictionary<string, ModelTuningStepState> Values { get; set; } = [];

    public static ModelTuningState CreateDefault() => new()
    {
        Values = new()
        {
            ["appearance"] = new() { Temperature = .4 },
            ["selection"] = new() { Temperature = .2 },
            ["planning"] = new() { Temperature = .4 },
            ["prose"] = new() { Temperature = .7 }
        }
    };
}

public sealed class ModelTuningStepState
{
    public double Temperature { get; set; }
    public string TopP { get; set; } = "";
    public string MaxTokens { get; set; } = "";
    public string Seed { get; set; } = "";
    public string FrequencyPenalty { get; set; } = "";
    public string PresencePenalty { get; set; } = "";
    public string StopSequences { get; set; } = "";
}
