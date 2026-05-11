namespace AgentRp.Data;

public sealed class StoryCardTemplateRow
{
    public string Id { get; set; } = "";
    public Guid OwnerUserId { get; set; }
    public string OwnerDisplayName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Instructions { get; set; } = "";
    public bool IsShared { get; set; }
    public DateTime? RetiredUtc { get; set; }
    public string ParentTemplateId { get; set; } = "";
    public string RootTemplateId { get; set; } = "";
    public int TemplateVersion { get; set; } = 1;
    public int DirectStoryCount { get; set; }
    public int DirectActiveTurnCount { get; set; }
    public int RemixCount { get; set; }
    public int RemixStoryCount { get; set; }
    public int RemixActiveTurnCount { get; set; }
    public DateTime? StatsRefreshedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class StoryCardTemplatePhaseRow : IStoryCardTemplateChildRow
{
    public string Id { get; set; } = "";
    public string StoryCardTemplateId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SetupInstructions { get; set; } = "";
    public string PlanningContext { get; set; } = "";
    public string EndCondition { get; set; } = "";
    public bool IsOptional { get; set; }
    public bool IsEnding { get; set; }
    public int SortOrder { get; set; }
}

public sealed class StoryCardTemplatePhaseTransitionRow : IStoryCardTemplateChildRow
{
    public string Id { get; set; } = "";
    public string StoryCardTemplateId { get; set; } = "";
    public string FromPhaseId { get; set; } = "";
    public string ToPhaseId { get; set; } = "";
    public string ConditionInstructions { get; set; } = "";
    public bool IsDefault { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class StoryCardTemplatePhaseRequirementRow : IStoryCardTemplateChildRow
{
    public string Id { get; set; } = "";
    public string StoryCardTemplateId { get; set; } = "";
    public string PhaseId { get; set; } = "";
    public string ChildCardType { get; set; } = "";
    public string ChildCardId { get; set; } = "";
    public int RequiredCount { get; set; } = 1;
    public int SortOrder { get; set; }
}

public sealed class StoryCardTemplateRoleRow : IStoryCardTemplateChildRow
{
    public string Id { get; set; } = "";
    public string StoryCardTemplateId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public string PrivateContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardTemplateItemRow : IStoryCardTemplateChildRow
{
    public string Id { get; set; } = "";
    public string StoryCardTemplateId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardTemplateLocationRow : IStoryCardTemplateChildRow
{
    public string Id { get; set; } = "";
    public string StoryCardTemplateId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstanceRow
{
    public string ChatId { get; set; } = "";
    public string Id { get; set; } = "";
    public string SourceTemplateId { get; set; } = "";
    public string ParentTemplateId { get; set; } = "";
    public string RootTemplateId { get; set; } = "";
    public string SourceOwnerDisplayName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string Status { get; set; } = "";
    public int StartTurnNumber { get; set; }
    public int? EndTurnNumber { get; set; }
    public string ActivePhaseId { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class StoryCardInstancePhaseRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SetupInstructions { get; set; } = "";
    public string PlanningContext { get; set; } = "";
    public string EndCondition { get; set; } = "";
    public bool IsOptional { get; set; }
    public bool IsEnding { get; set; }
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstancePhaseTransitionRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string FromPhaseId { get; set; } = "";
    public string ToPhaseId { get; set; } = "";
    public string ConditionInstructions { get; set; } = "";
    public bool IsDefault { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstancePhaseRequirementRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string PhaseId { get; set; } = "";
    public string ChildCardType { get; set; } = "";
    public string ChildCardId { get; set; } = "";
    public int RequiredCount { get; set; } = 1;
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstanceRoleRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public string PrivateContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstanceItemRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstanceLocationRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstanceAssignmentRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string ChildCardType { get; set; } = "";
    public string ChildCardId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityName { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardHistoryRow : IStoryCardInstanceChildRow
{
    public string ChatId { get; set; } = "";
    public string StoryCardInstanceId { get; set; } = "";
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public int TurnNumber { get; set; }
    public DateTime CreatedUtc { get; set; }
}
