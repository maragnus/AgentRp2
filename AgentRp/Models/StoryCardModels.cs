namespace AgentRp.Models;

public enum StoryCardStatus
{
    Active,
    Dormant,
    Concluded
}

public enum StoryCardHistoryKind
{
    Attached,
    Injected,
    StatusChanged,
    Edited
}

public enum StoryCardChildCardType
{
    Role,
    Location,
    Item
}

public sealed class StoryCardStats
{
    public int DirectStoryCount { get; set; }
    public int DirectActiveTurnCount { get; set; }
    public int RemixCount { get; set; }
    public int RemixStoryCount { get; set; }
    public int RemixActiveTurnCount { get; set; }
    public DateTime? RefreshedUtc { get; set; }
}

public sealed class StoryCardTemplate
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
    public StoryCardStats Stats { get; set; } = new();
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<PhaseCardTemplate> Phases { get; set; } = [];
    public List<PhaseTransitionTemplate> PhaseTransitions { get; set; } = [];
    public List<PhaseCardRequirementTemplate> PhaseRequirements { get; set; } = [];
    public List<RoleCardTemplate> Roles { get; set; } = [];
    public List<ItemCardTemplate> Items { get; set; } = [];
    public List<LocationCardTemplate> Locations { get; set; } = [];

    public bool CanApply => RetiredUtc is null && (IsShared || OwnerUserId != Guid.Empty);
}

public sealed class PhaseCardTemplate : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string SetupInstructions { get; set; } = "";
    public string PlanningContext { get; set; } = "";
    public string EndCondition { get; set; } = "";
    public bool IsOptional { get; set; }
    public bool IsEnding { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PhaseTransitionTemplate : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string FromPhaseId { get; set; } = "";
    public string ToPhaseId { get; set; } = "";
    public string ConditionInstructions { get; set; } = "";
    public bool IsDefault { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class PhaseCardRequirementTemplate : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string PhaseId { get; set; } = "";
    public StoryCardChildCardType ChildCardType { get; set; }
    public string ChildCardId { get; set; } = "";
    public int RequiredCount { get; set; } = 1;
    public int SortOrder { get; set; }
}

public sealed class RoleCardTemplate : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public string PrivateContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class ItemCardTemplate : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class LocationCardTemplate : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardInstance
{
    public string Id { get; set; } = "";
    public string ChatId { get; set; } = "";
    public string SourceTemplateId { get; set; } = "";
    public string ParentTemplateId { get; set; } = "";
    public string RootTemplateId { get; set; } = "";
    public string SourceOwnerDisplayName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Instructions { get; set; } = "";
    public StoryCardStatus Status { get; set; } = StoryCardStatus.Active;
    public int StartTurnNumber { get; set; }
    public int? EndTurnNumber { get; set; }
    public string ActivePhaseId { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<PhaseCardInstance> Phases { get; set; } = [];
    public List<PhaseTransitionInstance> PhaseTransitions { get; set; } = [];
    public List<PhaseCardRequirementInstance> PhaseRequirements { get; set; } = [];
    public List<RoleCardInstance> Roles { get; set; } = [];
    public List<ItemCardInstance> Items { get; set; } = [];
    public List<LocationCardInstance> Locations { get; set; } = [];
    public List<StoryCardEntityAssignment> Assignments { get; set; } = [];
    public List<StoryCardHistoryItem> History { get; set; } = [];
}

public sealed class PhaseCardInstance : IStoryCardChildModel
{
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

public sealed class PhaseTransitionInstance : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string FromPhaseId { get; set; } = "";
    public string ToPhaseId { get; set; } = "";
    public string ConditionInstructions { get; set; } = "";
    public bool IsDefault { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class PhaseCardRequirementInstance : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string PhaseId { get; set; } = "";
    public StoryCardChildCardType ChildCardType { get; set; }
    public string ChildCardId { get; set; } = "";
    public int RequiredCount { get; set; } = 1;
    public int SortOrder { get; set; }
}

public sealed class RoleCardInstance : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public string PrivateContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class ItemCardInstance : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class LocationCardInstance : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public string SourceTemplateChildId { get; set; } = "";
    public string Title { get; set; } = "";
    public string SelectionInstructions { get; set; } = "";
    public string CreationInstructions { get; set; } = "";
    public string OngoingContext { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardEntityAssignment : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public StoryCardChildCardType ChildCardType { get; set; }
    public string ChildCardId { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityName { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class StoryCardHistoryItem : IStoryCardChildModel
{
    public string Id { get; set; } = "";
    public StoryCardHistoryKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Details { get; set; } = "";
    public int TurnNumber { get; set; }
    public DateTime CreatedUtc { get; set; }
}
