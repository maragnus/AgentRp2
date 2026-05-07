using System.Text.Json.Nodes;

namespace AgentRp.Models;

public sealed class RpTranscriptState
{
    public int SchemaVersion { get; set; } = 1;
    public RpSceneFrame RootScene { get; set; } = new();
    public RpTranscriptOptionsState Options { get; set; } = new();
    public List<RpTranscriptTurn> Turns { get; set; } = [];
    public List<RpTranscriptSnapshot> Snapshots { get; set; } = [];
    public string ActiveLeafTurnId { get; set; } = "";
    public Dictionary<string, string> BranchSelections { get; set; } = [];
    public JsonObject Data { get; set; } = new();
}

public sealed class RpTranscriptOptionsState
{
    public bool InjectAudioTags { get; set; }
    public bool HideAudioTags { get; set; }
    public bool ShowAppearanceBlocks { get; set; }
    public bool ShowProcessTraces { get; set; }
    public bool AutoSpeakNewMessages { get; set; }
    public bool SpeakActionsInNarratorVoice { get; set; }
}

public sealed class RpTranscriptTurn
{
    public string Id { get; set; } = "";
    public string ParentTurnId { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string Mode { get; set; } = "";
    public string AuthorCharacterId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string ActorCharacterId { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string Guidance { get; set; } = "";
    public string Body { get; set; } = "";
    public RpTurnPlan Plan { get; set; } = new();
    public Dictionary<string, string> AppearanceByCharacterId { get; set; } = [];
    public Dictionary<string, string> PrivateIntentByCharacterId { get; set; } = [];
    public string SnapshotId { get; set; } = "";
    public RpMessageSpeechState Speech { get; set; } = new();
    public RpSceneFrame Scene { get; set; } = new();
    public RpTurnTrace? Trace { get; set; }
    public JsonObject Data { get; set; } = new();
}

public sealed class RpMessageSpeechState
{
    public string VoiceMessageId { get; set; } = "";
    public DateTime GeneratedUtc { get; set; }
    public string SourceHash { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ProviderType { get; set; } = "";
    public string ModelId { get; set; } = "";
    public Dictionary<string, string> VoiceIds { get; set; } = [];
}

public sealed class RpTranscriptSnapshot
{
    public string Id { get; set; } = "";
    public string TurnId { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public string Summary { get; set; } = "";
    public string EarlierPrivateIntentContinuity { get; set; } = "";
    public List<RpTranscriptSnapshotFact> Facts { get; set; } = [];
    public List<RpTranscriptSnapshotTimelineEntry> TimelineEntries { get; set; } = [];
    public Dictionary<string, string> CharacterAppearances { get; set; } = [];
    public RpSceneFrame Scene { get; set; } = new();
    public RpTurnTrace? Trace { get; set; }
    public JsonObject Data { get; set; } = new();
}

public sealed class RpTranscriptSnapshotFact
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
    public List<string> CharacterNames { get; set; } = [];
    public List<string> LocationNames { get; set; } = [];
    public List<string> ItemNames { get; set; } = [];
}

public sealed class RpTranscriptSnapshotTimelineEntry
{
    public string TimelineEntryId { get; set; } = "";
    public string WhenText { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
    public List<string> CharacterNames { get; set; } = [];
    public List<string> LocationNames { get; set; } = [];
    public List<string> ItemNames { get; set; } = [];
}

public sealed class RpSceneFrame
{
    public string LocationId { get; set; } = "";
    public string LocationName { get; set; } = "";
    public List<string> InSceneCharacterIds { get; set; } = [];
    public List<string> InSceneItemIds { get; set; } = [];
    public JsonObject Data { get; set; } = new();
}

public sealed class RpTurnPlan
{
    public string TurnShape { get; set; } = "";
    public string Beat { get; set; } = "";
    public string Intent { get; set; } = "";
    public string ImmediateGoal { get; set; } = "";
    public string WhyNow { get; set; } = "";
    public string ChangeIntroduced { get; set; } = "";
    public string Guardrails { get; set; } = "";
    public JsonObject Data { get; set; } = new();
}

public sealed class RpTurnTrace
{
    public string Summary { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ProviderType { get; set; } = "";
    public string ModelId { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public double DurationSeconds { get; set; }
    public List<RpTurnTraceStep> Steps { get; set; } = [];
    public JsonObject Data { get; set; } = new();
}

public sealed class RpTurnTraceStep
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ProviderType { get; set; } = "";
    public string ModelId { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public double DurationSeconds { get; set; }
    public string SystemPrompt { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public string RawOutput { get; set; } = "";
    public string StructuredOutputJson { get; set; } = "";
    public string Error { get; set; } = "";
    public JsonObject Data { get; set; } = new();
}
