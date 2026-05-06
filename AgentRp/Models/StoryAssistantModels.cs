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

public sealed class StoryAssistantState
{
    public int SchemaVersion { get; set; } = 1;
    public StoryAssistantReviewMode ReviewMode { get; set; } = StoryAssistantReviewMode.ReviewAll;
    public string ConversationId { get; set; } = "";
    public List<StoryAssistantTranscriptItem> Items { get; set; } = [];
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
    public StoryAssistantQuestion Question { get; set; } = new();
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
    public List<StoryAssistantQuestionChoice> Choices { get; set; } = [];
    public string Answer { get; set; } = "";
}

public sealed class StoryAssistantQuestionChoice
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed record StoryAssistantDecision(StoryAssistantDecisionKind Kind, string Reason);
