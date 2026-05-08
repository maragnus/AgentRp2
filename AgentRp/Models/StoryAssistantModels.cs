using System.Text.Json.Nodes;

namespace AgentRp.Models;

public enum StoryAssistantReviewMode
{
    ReviewAll,
    ReviewMajor,
    AutoApprove
}

public enum StoryAssistantItemKind
{
    UserMessage,
    AssistantMessage,
    ToolCall,
    Question
}

public enum StoryAssistantOperationKind
{
    None,
    Read,
    Create,
    Update,
    Question
}

public enum StoryAssistantItemStatus
{
    Pending,
    Streaming,
    Read,
    Applied,
    NeedsReview,
    Accepted,
    RetryRequested,
    Rejected,
    Answered,
    Stopped,
    Failed
}

public enum StoryAssistantDecisionKind
{
    Accept,
    TryAgain,
    Reject
}

public enum StoryAssistantChangeRisk
{
    Low,
    Major,
    Destructive,
    Blocked
}

public enum StoryAssistantQuestionSelectionMode
{
    Single,
    Multiple
}

public sealed class StoryAssistantState
{
    public int SchemaVersion { get; set; } = 2;
    public StoryAssistantReviewMode ReviewMode { get; set; } = StoryAssistantReviewMode.ReviewAll;
    public string LastResponseId { get; set; } = "";
    public List<string> ResponseIds { get; set; } = [];
    public string ResponseProviderId { get; set; } = "";
    public string ResponseModelId { get; set; } = "";
    public bool RemoteThreadLost { get; set; }
    public string RemoteThreadError { get; set; } = "";
    public List<StoryAssistantTranscriptItem> Items { get; set; } = [];
    public List<StoryAssistantWorkItem> WorkItems { get; set; } = [];
}

public sealed class StoryAssistantTranscriptItem
{
    public string Id { get; set; } = "";
    public StoryAssistantItemKind Kind { get; set; }
    public StoryAssistantItemStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string Text { get; set; } = "";
    public string Title { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string ToolCallId { get; set; } = "";
    public string WorkItemId { get; set; } = "";
    public StoryAssistantOperationKind Operation { get; set; }
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string ArgumentsJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public JsonObject Before { get; set; } = new();
    public JsonObject After { get; set; } = new();
    public List<StoryAssistantFieldDiff> Diffs { get; set; } = [];
    public StoryAssistantChangeRisk Risk { get; set; } = StoryAssistantChangeRisk.Low;
    public string DecisionReason { get; set; } = "";
    public StoryAssistantRetryContext Retry { get; set; } = new();
    public StoryAssistantDiagnostics Diagnostics { get; set; } = new();
    public StoryAssistantQuestion Question { get; set; } = new();
}

public sealed class StoryAssistantRetryContext
{
    public string DisplayMessage { get; set; } = "";
    public string ModelInput { get; set; } = "";
    public bool IsRetry { get; set; }

    public bool CanRetry => !string.IsNullOrWhiteSpace(ModelInput);
}

public sealed class StoryAssistantDiagnostics
{
    public string Outcome { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string PreviousResponseId { get; set; } = "";
    public string ResponseId { get; set; } = "";
    public string RequestDisplay { get; set; } = "";
    public string LastStreamEvent { get; set; } = "";
    public int ToolRoundCount { get; set; }
    public string Error { get; set; } = "";
    public DateTime RecordedUtc { get; set; }

    public bool HasDetails =>
        !string.IsNullOrWhiteSpace(Outcome)
        || !string.IsNullOrWhiteSpace(Reason)
        || !string.IsNullOrWhiteSpace(Error)
        || !string.IsNullOrWhiteSpace(ProviderName)
        || !string.IsNullOrWhiteSpace(ModelId)
        || !string.IsNullOrWhiteSpace(ResponseId)
        || !string.IsNullOrWhiteSpace(PreviousResponseId)
        || !string.IsNullOrWhiteSpace(LastStreamEvent);
}

public sealed class StoryAssistantFieldDiff
{
    public string Field { get; set; } = "";
    public string Label { get; set; } = "";
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
}

public sealed class StoryAssistantQuestion
{
    public string Prompt { get; set; } = "";
    public bool AllowsFreeform { get; set; }
    public StoryAssistantQuestionSelectionMode SelectionMode { get; set; } = StoryAssistantQuestionSelectionMode.Single;
    public int MinSelections { get; set; } = 1;
    public int MaxSelections { get; set; } = 1;
    public List<StoryAssistantQuestionChoice> Choices { get; set; } = [];
    public string Answer { get; set; } = "";
}

public sealed class StoryAssistantQuestionChoice
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed record StoryAssistantDecision(StoryAssistantDecisionKind Kind, string Reason);
