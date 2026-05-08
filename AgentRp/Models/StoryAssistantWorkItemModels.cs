using System.Text.Json.Nodes;

namespace AgentRp.Models;

public enum StoryAssistantWorkItemKind
{
    Question,
    MutationReview,
    SceneReview
}

public enum StoryAssistantWorkItemStatus
{
    Pending,
    Completed,
    RetryRequested,
    Rejected,
    Cancelled,
    Conflict,
    Failed
}

public enum StoryAssistantWorkItemResolutionKind
{
    Answer,
    Accept,
    TryAgain,
    Reject
}

public sealed class StoryAssistantWorkItem
{
    public string Id { get; set; } = "";
    public string TranscriptItemId { get; set; } = "";
    public StoryAssistantWorkItemKind Kind { get; set; }
    public StoryAssistantWorkItemStatus Status { get; set; } = StoryAssistantWorkItemStatus.Pending;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string Title { get; set; } = "";
    public string ToolName { get; set; } = "";
    public string ToolCallId { get; set; } = "";
    public string AwaitingResponseId { get; set; } = "";
    public string ResponseProviderId { get; set; } = "";
    public string ResponseModelId { get; set; } = "";
    public string EntityArea { get; set; } = "";
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
    public StoryAssistantDiagnostics Diagnostics { get; set; } = new();
}

public sealed record StoryAssistantWorkItemResolution(
    StoryAssistantWorkItemResolutionKind Kind,
    string Answer,
    string Reason);

