using AgentRp.Models;

namespace AgentRp.Components.StoryCards;

public sealed record StoryCardChildReference(StoryCardChildCardType ChildCardType, string ChildCardId);

public sealed record StoryCardExistingRequirementRequest(string PhaseId, StoryCardChildCardType ChildCardType, string ChildCardId);

public sealed record StoryCardNewRequirementRequest(string PhaseId, StoryCardChildCardType ChildCardType, string Title);

public sealed class StoryCardPhaseEditorRow(
    string id,
    Func<string> getTitle,
    Action<string> setTitle,
    Func<string> getSetupInstructions,
    Action<string> setSetupInstructions,
    Func<string> getPlanningContext,
    Action<string> setPlanningContext,
    Func<string> getEndCondition,
    Action<string> setEndCondition,
    Func<bool> getIsOptional,
    Action<bool> setIsOptional,
    Func<bool> getIsEnding,
    Action<bool> setIsEnding)
{
    public string Id { get; } = id;
    public string Title { get => getTitle(); set => setTitle(value); }
    public string SetupInstructions { get => getSetupInstructions(); set => setSetupInstructions(value); }
    public string PlanningContext { get => getPlanningContext(); set => setPlanningContext(value); }
    public string EndCondition { get => getEndCondition(); set => setEndCondition(value); }
    public bool IsOptional { get => getIsOptional(); set => setIsOptional(value); }
    public bool IsEnding { get => getIsEnding(); set => setIsEnding(value); }

    public static StoryCardPhaseEditorRow Create(PhaseCardTemplate phase) => new(
        phase.Id,
        () => phase.Title,
        value => phase.Title = value,
        () => phase.SetupInstructions,
        value => phase.SetupInstructions = value,
        () => phase.PlanningContext,
        value => phase.PlanningContext = value,
        () => phase.EndCondition,
        value => phase.EndCondition = value,
        () => phase.IsOptional,
        value => phase.IsOptional = value,
        () => phase.IsEnding,
        value => phase.IsEnding = value);

    public static StoryCardPhaseEditorRow Create(PhaseCardInstance phase) => new(
        phase.Id,
        () => phase.Title,
        value => phase.Title = value,
        () => phase.SetupInstructions,
        value => phase.SetupInstructions = value,
        () => phase.PlanningContext,
        value => phase.PlanningContext = value,
        () => phase.EndCondition,
        value => phase.EndCondition = value,
        () => phase.IsOptional,
        value => phase.IsOptional = value,
        () => phase.IsEnding,
        value => phase.IsEnding = value);
}

public sealed class StoryCardRequirementEditorRow(
    string id,
    string phaseId,
    StoryCardChildCardType childCardType,
    string childCardId,
    Func<int> getRequiredCount,
    Action<int> setRequiredCount,
    Func<string> getChildTitle)
{
    public string Id { get; } = id;
    public string PhaseId { get; } = phaseId;
    public StoryCardChildCardType ChildCardType { get; } = childCardType;
    public string ChildCardId { get; } = childCardId;
    public int RequiredCount { get => getRequiredCount(); set => setRequiredCount(Math.Max(1, value)); }
    public string ChildTitle => string.IsNullOrWhiteSpace(getChildTitle()) ? ChildCardType.ToString() : getChildTitle();

    public static StoryCardRequirementEditorRow Create(PhaseCardRequirementTemplate requirement, StoryCardTemplate template) => new(
        requirement.Id,
        requirement.PhaseId,
        requirement.ChildCardType,
        requirement.ChildCardId,
        () => requirement.RequiredCount,
        value => requirement.RequiredCount = Math.Max(1, value),
        () => ResolveChildTitle(template, requirement.ChildCardType, requirement.ChildCardId));

    public static StoryCardRequirementEditorRow Create(PhaseCardRequirementInstance requirement, StoryCardInstance instance) => new(
        requirement.Id,
        requirement.PhaseId,
        requirement.ChildCardType,
        requirement.ChildCardId,
        () => requirement.RequiredCount,
        value => requirement.RequiredCount = Math.Max(1, value),
        () => ResolveChildTitle(instance, requirement.ChildCardType, requirement.ChildCardId));

    static string ResolveChildTitle(StoryCardTemplate template, StoryCardChildCardType type, string childCardId) => type switch
    {
        StoryCardChildCardType.Role => template.Roles.FirstOrDefault(child => child.Id == childCardId)?.Title ?? "",
        StoryCardChildCardType.Item => template.Items.FirstOrDefault(child => child.Id == childCardId)?.Title ?? "",
        StoryCardChildCardType.Location => template.Locations.FirstOrDefault(child => child.Id == childCardId)?.Title ?? "",
        _ => ""
    };

    static string ResolveChildTitle(StoryCardInstance instance, StoryCardChildCardType type, string childCardId) => type switch
    {
        StoryCardChildCardType.Role => instance.Roles.FirstOrDefault(child => child.Id == childCardId)?.Title ?? "",
        StoryCardChildCardType.Item => instance.Items.FirstOrDefault(child => child.Id == childCardId)?.Title ?? "",
        StoryCardChildCardType.Location => instance.Locations.FirstOrDefault(child => child.Id == childCardId)?.Title ?? "",
        _ => ""
    };
}

public sealed class StoryCardChildEditorRow(
    string id,
    string fallbackTitle,
    bool includePrivate,
    bool hasAssignment,
    string entityType,
    Func<string> getTitle,
    Action<string> setTitle,
    Func<string> getSelectionInstructions,
    Action<string> setSelectionInstructions,
    Func<string> getCreationInstructions,
    Action<string> setCreationInstructions,
    Func<string> getOngoingContext,
    Action<string> setOngoingContext,
    Func<string> getPrivateContext,
    Action<string> setPrivateContext,
    Func<string> getAssignmentText)
{
    public string Id { get; } = id;
    public string FallbackTitle { get; } = fallbackTitle;
    public bool IncludePrivate { get; } = includePrivate;
    public bool HasAssignment { get; } = hasAssignment;
    public string Title { get => getTitle(); set => setTitle(value); }
    public string SelectionInstructions { get => getSelectionInstructions(); set => setSelectionInstructions(value); }
    public string CreationInstructions { get => getCreationInstructions(); set => setCreationInstructions(value); }
    public string OngoingContext { get => getOngoingContext(); set => setOngoingContext(value); }
    public string PrivateContext { get => getPrivateContext(); set => setPrivateContext(value); }
    public string AssignmentText
    {
        get
        {
            var text = getAssignmentText();
            return string.IsNullOrWhiteSpace(text) ? $"No {entityType} assigned" : text;
        }
    }

    public static StoryCardChildEditorRow Create(RoleCardTemplate role) => Create(
        role.Id,
        "Role",
        true,
        false,
        "character",
        () => role.Title,
        value => role.Title = value,
        () => role.SelectionInstructions,
        value => role.SelectionInstructions = value,
        () => role.CreationInstructions,
        value => role.CreationInstructions = value,
        () => role.OngoingContext,
        value => role.OngoingContext = value,
        () => role.PrivateContext,
        value => role.PrivateContext = value);

    public static StoryCardChildEditorRow Create(RoleCardInstance role, IReadOnlyList<StoryCardEntityAssignment> assignments) => Create(
        role.Id,
        "Role",
        true,
        true,
        "character",
        () => role.Title,
        value => role.Title = value,
        () => role.SelectionInstructions,
        value => role.SelectionInstructions = value,
        () => role.CreationInstructions,
        value => role.CreationInstructions = value,
        () => role.OngoingContext,
        value => role.OngoingContext = value,
        () => role.PrivateContext,
        value => role.PrivateContext = value,
        () => AssignmentSummary(role.Id, StoryCardChildCardType.Role, "character", assignments));

    public static StoryCardChildEditorRow Create(ItemCardTemplate item) => Create(
        item.Id,
        "Item",
        false,
        false,
        "item",
        () => item.Title,
        value => item.Title = value,
        () => item.SelectionInstructions,
        value => item.SelectionInstructions = value,
        () => item.CreationInstructions,
        value => item.CreationInstructions = value,
        () => item.OngoingContext,
        value => item.OngoingContext = value);

    public static StoryCardChildEditorRow Create(ItemCardInstance item, IReadOnlyList<StoryCardEntityAssignment> assignments) => Create(
        item.Id,
        "Item",
        false,
        true,
        "item",
        () => item.Title,
        value => item.Title = value,
        () => item.SelectionInstructions,
        value => item.SelectionInstructions = value,
        () => item.CreationInstructions,
        value => item.CreationInstructions = value,
        () => item.OngoingContext,
        value => item.OngoingContext = value,
        () => AssignmentSummary(item.Id, StoryCardChildCardType.Item, "item", assignments));

    public static StoryCardChildEditorRow Create(LocationCardTemplate location) => Create(
        location.Id,
        "Location",
        false,
        false,
        "location",
        () => location.Title,
        value => location.Title = value,
        () => location.SelectionInstructions,
        value => location.SelectionInstructions = value,
        () => location.CreationInstructions,
        value => location.CreationInstructions = value,
        () => location.OngoingContext,
        value => location.OngoingContext = value);

    public static StoryCardChildEditorRow Create(LocationCardInstance location, IReadOnlyList<StoryCardEntityAssignment> assignments) => Create(
        location.Id,
        "Location",
        false,
        true,
        "location",
        () => location.Title,
        value => location.Title = value,
        () => location.SelectionInstructions,
        value => location.SelectionInstructions = value,
        () => location.CreationInstructions,
        value => location.CreationInstructions = value,
        () => location.OngoingContext,
        value => location.OngoingContext = value,
        () => AssignmentSummary(location.Id, StoryCardChildCardType.Location, "location", assignments));

    static StoryCardChildEditorRow Create(
        string id,
        string fallbackTitle,
        bool includePrivate,
        bool hasAssignment,
        string entityType,
        Func<string> getTitle,
        Action<string> setTitle,
        Func<string> getSelectionInstructions,
        Action<string> setSelectionInstructions,
        Func<string> getCreationInstructions,
        Action<string> setCreationInstructions,
        Func<string> getOngoingContext,
        Action<string> setOngoingContext,
        Func<string>? getPrivateContext = null,
        Action<string>? setPrivateContext = null,
        Func<string>? getAssignmentText = null) => new(
            id,
            fallbackTitle,
            includePrivate,
            hasAssignment,
            entityType,
            getTitle,
            setTitle,
            getSelectionInstructions,
            setSelectionInstructions,
            getCreationInstructions,
            setCreationInstructions,
            getOngoingContext,
            setOngoingContext,
            getPrivateContext ?? (() => ""),
            setPrivateContext ?? (_ => { }),
            getAssignmentText ?? (() => ""));

    static string AssignmentSummary(
        string childCardId,
        StoryCardChildCardType type,
        string entityType,
        IReadOnlyList<StoryCardEntityAssignment> assignments)
    {
        var names = assignments
            .Where(assignment => assignment.ChildCardType == type && assignment.ChildCardId == childCardId)
            .Select(assignment => assignment.EntityName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        return names.Count switch
        {
            0 => $"No {entityType} assigned",
            1 => names[0],
            _ => $"{names.Count} {Plural(entityType)} assigned"
        };
    }

    static string Plural(string value) => value switch
    {
        "character" => "characters",
        "location" => "locations",
        _ => $"{value}s"
    };
}
