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
    public RpTranscriptState Transcript { get; set; } = new();
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
            ["snapshot"] = new() { System = "You summarize the state of an interactive roleplay scene for future continuation. Return concise JSON only.", User = "{context.transcript}\n\n{context.characterAppearances}\n\nSummarize the scene state for a pinned checkpoint." },
            ["appearance"] = new() { System = "You update character scene state. Return JSON only.", User = "Characters:\n{appearance.characters}\n\nTranscript:\n{appearance.transcript}" },
            ["selection"] = new() { System = "Choose the next responder from the active scene. Return JSON only.", User = "{context.transcript}\n\nPresent: {context.characters}\n\nWho should respond next?" },
            ["planning"] = new() { System = "Produce a structured dramatic plan before prose. Return JSON only.", User = "{context.snapshot}\n\n{context.transcript}\n\n{context.characterAppearances}\n\nActor: {actor.name}\n\n{guidanceSection}\n\n{requestedTurnShapeSection}\n\n{turnScopeRules}" },
            ["prose"] = new() { System = "Write polished contemporary roleplay prose.", User = "{context.snapshot}\n\n{context.transcript}\n\n{context.characterAppearances}\n\nActor: {actor.name}\n\n{guidanceSection}\n\n{requestedTurnShapeSection}\n\n{planning.output}" }
        },
        TurnShapes = new()
        {
            ["planning"] = Shapes("Compact", "Brief", "Extended", "Monologue"),
            ["prose"] = Shapes("Compact", "Brief", "Extended", "Monologue", "Silent", "Silent Extended")
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
