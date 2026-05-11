using AgentRp.Data;
using AgentRp.Models;

namespace AgentRp.Session;

internal static class StoryCardPersistenceMapper
{
    public static StoryCardTemplate ToTemplate(
        StoryCardTemplateRow row,
        IEnumerable<StoryCardTemplatePhaseRow> phases,
        IEnumerable<StoryCardTemplatePhaseTransitionRow> transitions,
        IEnumerable<StoryCardTemplatePhaseRequirementRow> requirements,
        IEnumerable<StoryCardTemplateRoleRow> roles,
        IEnumerable<StoryCardTemplateItemRow> items,
        IEnumerable<StoryCardTemplateLocationRow> locations) => new()
    {
        Id = row.Id,
        OwnerUserId = row.OwnerUserId,
        OwnerDisplayName = row.OwnerDisplayName,
        Title = row.Title,
        Summary = row.Summary,
        Instructions = row.Instructions,
        IsShared = row.IsShared,
        RetiredUtc = row.RetiredUtc,
        ParentTemplateId = row.ParentTemplateId,
        RootTemplateId = row.RootTemplateId,
        TemplateVersion = row.TemplateVersion,
        CreatedUtc = row.CreatedUtc,
        UpdatedUtc = row.UpdatedUtc,
        Stats = new()
        {
            DirectStoryCount = row.DirectStoryCount,
            DirectActiveTurnCount = row.DirectActiveTurnCount,
            RemixCount = row.RemixCount,
            RemixStoryCount = row.RemixStoryCount,
            RemixActiveTurnCount = row.RemixActiveTurnCount,
            RefreshedUtc = row.StatsRefreshedUtc
        },
        Phases = phases.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        PhaseTransitions = transitions.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        PhaseRequirements = requirements.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Roles = roles.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Items = items.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Locations = locations.OrderBy(x => x.SortOrder).Select(ToModel).ToList()
    };

    public static void Apply(StoryCardTemplate model, StoryCardTemplateRow row, DateTime now)
    {
        row.Id = model.Id;
        row.OwnerUserId = model.OwnerUserId;
        row.OwnerDisplayName = model.OwnerDisplayName;
        row.Title = model.Title;
        row.Summary = model.Summary;
        row.Instructions = model.Instructions;
        row.IsShared = model.IsShared;
        row.RetiredUtc = model.RetiredUtc;
        row.ParentTemplateId = model.ParentTemplateId;
        row.RootTemplateId = model.RootTemplateId;
        row.TemplateVersion = Math.Max(1, model.TemplateVersion);
        row.DirectStoryCount = model.Stats.DirectStoryCount;
        row.DirectActiveTurnCount = model.Stats.DirectActiveTurnCount;
        row.RemixCount = model.Stats.RemixCount;
        row.RemixStoryCount = model.Stats.RemixStoryCount;
        row.RemixActiveTurnCount = model.Stats.RemixActiveTurnCount;
        row.StatsRefreshedUtc = model.Stats.RefreshedUtc;
        row.UpdatedUtc = now;
    }

    public static StoryCardInstance ToInstance(
        StoryCardInstanceRow row,
        IEnumerable<StoryCardInstancePhaseRow> phases,
        IEnumerable<StoryCardInstancePhaseTransitionRow> transitions,
        IEnumerable<StoryCardInstancePhaseRequirementRow> requirements,
        IEnumerable<StoryCardInstanceRoleRow> roles,
        IEnumerable<StoryCardInstanceItemRow> items,
        IEnumerable<StoryCardInstanceLocationRow> locations,
        IEnumerable<StoryCardInstanceAssignmentRow> assignments,
        IEnumerable<StoryCardHistoryRow> history) => new()
    {
        Id = row.Id,
        ChatId = row.ChatId,
        SourceTemplateId = row.SourceTemplateId,
        ParentTemplateId = row.ParentTemplateId,
        RootTemplateId = row.RootTemplateId,
        SourceOwnerDisplayName = row.SourceOwnerDisplayName,
        Title = row.Title,
        Summary = row.Summary,
        Instructions = row.Instructions,
        Status = Enum.TryParse<StoryCardStatus>(row.Status, out var status) ? status : StoryCardStatus.Active,
        StartTurnNumber = row.StartTurnNumber,
        EndTurnNumber = row.EndTurnNumber,
        ActivePhaseId = row.ActivePhaseId,
        CreatedUtc = row.CreatedUtc,
        UpdatedUtc = row.UpdatedUtc,
        Phases = phases.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        PhaseTransitions = transitions.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        PhaseRequirements = requirements.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Roles = roles.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Items = items.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Locations = locations.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Assignments = assignments.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        History = history.OrderByDescending(x => x.CreatedUtc).Select(ToModel).ToList()
    };

    public static void Apply(StoryCardInstance model, StoryCardInstanceRow row, int sortOrder, DateTime now)
    {
        row.ChatId = model.ChatId;
        row.Id = model.Id;
        row.SourceTemplateId = model.SourceTemplateId;
        row.ParentTemplateId = model.ParentTemplateId;
        row.RootTemplateId = model.RootTemplateId;
        row.SourceOwnerDisplayName = model.SourceOwnerDisplayName;
        row.Title = model.Title;
        row.Summary = model.Summary;
        row.Instructions = model.Instructions;
        row.Status = model.Status.ToString();
        row.StartTurnNumber = model.StartTurnNumber;
        row.EndTurnNumber = model.EndTurnNumber;
        row.ActivePhaseId = model.ActivePhaseId;
        row.SortOrder = sortOrder;
        row.UpdatedUtc = now;
    }

    public static StoryCardInstance CloneToInstance(StoryCardTemplate template, string chatId, int startTurnNumber, bool injected)
    {
        var instanceId = $"story-card-{Guid.NewGuid():N}";
        var instance = new StoryCardInstance
        {
            Id = instanceId,
            ChatId = chatId,
            SourceTemplateId = template.Id,
            ParentTemplateId = template.ParentTemplateId,
            RootTemplateId = string.IsNullOrWhiteSpace(template.RootTemplateId) ? template.Id : template.RootTemplateId,
            SourceOwnerDisplayName = template.OwnerDisplayName,
            Title = template.Title,
            Summary = template.Summary,
            Instructions = template.Instructions,
            Status = StoryCardStatus.Active,
            StartTurnNumber = Math.Max(0, startTurnNumber),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Phases = template.Phases.Select(ClonePhase).ToList(),
            Roles = template.Roles.Select(CloneRole).ToList(),
            Items = template.Items.Select(CloneItem).ToList(),
            Locations = template.Locations.Select(CloneLocation).ToList()
        };

        instance.ActivePhaseId = instance.Phases.OrderBy(phase => phase.SortOrder).FirstOrDefault()?.Id ?? "";
        instance.PhaseTransitions = CloneTransitions(template, instance.Phases);
        instance.PhaseRequirements = CloneRequirements(template, instance);
        instance.History.Add(new()
        {
            Id = $"history-{Guid.NewGuid():N}",
            Kind = injected ? StoryCardHistoryKind.Injected : StoryCardHistoryKind.Attached,
            Title = injected ? "Story card injected" : "Story card attached",
            Details = template.Title,
            TurnNumber = Math.Max(0, startTurnNumber),
            CreatedUtc = DateTime.UtcNow
        });
        return instance;
    }

    public static void EnsureLinearTransitions(StoryCardTemplate template)
    {
        var phases = template.Phases.OrderBy(phase => phase.SortOrder).ToList();
        var validPhaseIds = phases.Select(phase => phase.Id).ToHashSet(StringComparer.Ordinal);
        template.PhaseTransitions.RemoveAll(transition => !validPhaseIds.Contains(transition.FromPhaseId) || !validPhaseIds.Contains(transition.ToPhaseId));

        for (var index = 0; index < phases.Count - 1; index++)
        {
            var from = phases[index];
            var to = phases[index + 1];
            if (template.PhaseTransitions.Any(transition => transition.FromPhaseId == from.Id && transition.IsDefault))
                continue;

            template.PhaseTransitions.Add(new()
            {
                Id = $"transition-{Guid.NewGuid():N}",
                FromPhaseId = from.Id,
                ToPhaseId = to.Id,
                IsDefault = true,
                SortOrder = index
            });
        }

        for (var index = 0; index < template.PhaseTransitions.Count; index++)
            template.PhaseTransitions[index].SortOrder = index;
    }

    static PhaseCardTemplate ToModel(StoryCardTemplatePhaseRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        SetupInstructions = row.SetupInstructions,
        PlanningContext = row.PlanningContext,
        EndCondition = row.EndCondition,
        IsOptional = row.IsOptional,
        IsEnding = row.IsEnding,
        SortOrder = row.SortOrder
    };

    static PhaseTransitionTemplate ToModel(StoryCardTemplatePhaseTransitionRow row) => new()
    {
        Id = row.Id,
        FromPhaseId = row.FromPhaseId,
        ToPhaseId = row.ToPhaseId,
        ConditionInstructions = row.ConditionInstructions,
        IsDefault = row.IsDefault,
        SortOrder = row.SortOrder
    };

    static PhaseCardRequirementTemplate ToModel(StoryCardTemplatePhaseRequirementRow row) => new()
    {
        Id = row.Id,
        PhaseId = row.PhaseId,
        ChildCardType = ParseChildCardType(row.ChildCardType),
        ChildCardId = row.ChildCardId,
        RequiredCount = Math.Max(1, row.RequiredCount),
        SortOrder = row.SortOrder
    };

    static RoleCardTemplate ToModel(StoryCardTemplateRoleRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        SelectionInstructions = row.SelectionInstructions,
        CreationInstructions = row.CreationInstructions,
        OngoingContext = row.OngoingContext,
        PrivateContext = row.PrivateContext,
        SortOrder = row.SortOrder
    };

    static ItemCardTemplate ToModel(StoryCardTemplateItemRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        SelectionInstructions = row.SelectionInstructions,
        CreationInstructions = row.CreationInstructions,
        OngoingContext = row.OngoingContext,
        SortOrder = row.SortOrder
    };

    static LocationCardTemplate ToModel(StoryCardTemplateLocationRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        SelectionInstructions = row.SelectionInstructions,
        CreationInstructions = row.CreationInstructions,
        OngoingContext = row.OngoingContext,
        SortOrder = row.SortOrder
    };

    static PhaseCardInstance ToModel(StoryCardInstancePhaseRow row) => new()
    {
        Id = row.Id,
        SourceTemplateChildId = row.SourceTemplateChildId,
        Title = row.Title,
        SetupInstructions = row.SetupInstructions,
        PlanningContext = row.PlanningContext,
        EndCondition = row.EndCondition,
        IsOptional = row.IsOptional,
        IsEnding = row.IsEnding,
        SortOrder = row.SortOrder
    };

    static PhaseTransitionInstance ToModel(StoryCardInstancePhaseTransitionRow row) => new()
    {
        Id = row.Id,
        FromPhaseId = row.FromPhaseId,
        ToPhaseId = row.ToPhaseId,
        ConditionInstructions = row.ConditionInstructions,
        IsDefault = row.IsDefault,
        SortOrder = row.SortOrder
    };

    static PhaseCardRequirementInstance ToModel(StoryCardInstancePhaseRequirementRow row) => new()
    {
        Id = row.Id,
        SourceTemplateChildId = row.SourceTemplateChildId,
        PhaseId = row.PhaseId,
        ChildCardType = ParseChildCardType(row.ChildCardType),
        ChildCardId = row.ChildCardId,
        RequiredCount = Math.Max(1, row.RequiredCount),
        SortOrder = row.SortOrder
    };

    static RoleCardInstance ToModel(StoryCardInstanceRoleRow row) => new()
    {
        Id = row.Id,
        SourceTemplateChildId = row.SourceTemplateChildId,
        Title = row.Title,
        SelectionInstructions = row.SelectionInstructions,
        CreationInstructions = row.CreationInstructions,
        OngoingContext = row.OngoingContext,
        PrivateContext = row.PrivateContext,
        SortOrder = row.SortOrder
    };

    static ItemCardInstance ToModel(StoryCardInstanceItemRow row) => new()
    {
        Id = row.Id,
        SourceTemplateChildId = row.SourceTemplateChildId,
        Title = row.Title,
        SelectionInstructions = row.SelectionInstructions,
        CreationInstructions = row.CreationInstructions,
        OngoingContext = row.OngoingContext,
        SortOrder = row.SortOrder
    };

    static LocationCardInstance ToModel(StoryCardInstanceLocationRow row) => new()
    {
        Id = row.Id,
        SourceTemplateChildId = row.SourceTemplateChildId,
        Title = row.Title,
        SelectionInstructions = row.SelectionInstructions,
        CreationInstructions = row.CreationInstructions,
        OngoingContext = row.OngoingContext,
        SortOrder = row.SortOrder
    };

    static StoryCardEntityAssignment ToModel(StoryCardInstanceAssignmentRow row) => new()
    {
        Id = row.Id,
        ChildCardType = ParseChildCardType(row.ChildCardType),
        ChildCardId = row.ChildCardId,
        EntityId = row.EntityId,
        EntityName = row.EntityName,
        SortOrder = row.SortOrder
    };

    static StoryCardHistoryItem ToModel(StoryCardHistoryRow row) => new()
    {
        Id = row.Id,
        Kind = Enum.TryParse<StoryCardHistoryKind>(row.Kind, out var kind) ? kind : StoryCardHistoryKind.Edited,
        Title = row.Title,
        Details = row.Details,
        TurnNumber = row.TurnNumber,
        CreatedUtc = row.CreatedUtc
    };

    static PhaseCardInstance ClonePhase(PhaseCardTemplate value) => new()
    {
        Id = $"phase-{Guid.NewGuid():N}",
        SourceTemplateChildId = value.Id,
        Title = value.Title,
        SetupInstructions = value.SetupInstructions,
        PlanningContext = value.PlanningContext,
        EndCondition = value.EndCondition,
        IsOptional = value.IsOptional,
        IsEnding = value.IsEnding,
        SortOrder = value.SortOrder
    };

    static List<PhaseTransitionInstance> CloneTransitions(StoryCardTemplate template, IReadOnlyList<PhaseCardInstance> phases)
    {
        var phaseIdsBySource = phases.ToDictionary(phase => phase.SourceTemplateChildId, phase => phase.Id, StringComparer.Ordinal);
        return template.PhaseTransitions
            .Where(transition => phaseIdsBySource.ContainsKey(transition.FromPhaseId) && phaseIdsBySource.ContainsKey(transition.ToPhaseId))
            .OrderBy(transition => transition.SortOrder)
            .Select(transition => new PhaseTransitionInstance
            {
                Id = $"transition-{Guid.NewGuid():N}",
                FromPhaseId = phaseIdsBySource[transition.FromPhaseId],
                ToPhaseId = phaseIdsBySource[transition.ToPhaseId],
                ConditionInstructions = transition.ConditionInstructions,
                IsDefault = transition.IsDefault,
                SortOrder = transition.SortOrder
            })
            .ToList();
    }

    static List<PhaseCardRequirementInstance> CloneRequirements(StoryCardTemplate template, StoryCardInstance instance)
    {
        var phaseIdsBySource = instance.Phases.ToDictionary(phase => phase.SourceTemplateChildId, phase => phase.Id, StringComparer.Ordinal);
        var roleIdsBySource = instance.Roles.ToDictionary(role => role.SourceTemplateChildId, role => role.Id, StringComparer.Ordinal);
        var itemIdsBySource = instance.Items.ToDictionary(item => item.SourceTemplateChildId, item => item.Id, StringComparer.Ordinal);
        var locationIdsBySource = instance.Locations.ToDictionary(location => location.SourceTemplateChildId, location => location.Id, StringComparer.Ordinal);

        return template.PhaseRequirements
            .Where(requirement => phaseIdsBySource.ContainsKey(requirement.PhaseId))
            .Select(requirement => CloneRequirement(requirement, phaseIdsBySource, roleIdsBySource, itemIdsBySource, locationIdsBySource))
            .Where(requirement => requirement is not null)
            .Select(requirement => requirement!)
            .OrderBy(requirement => requirement.SortOrder)
            .ToList();
    }

    static PhaseCardRequirementInstance? CloneRequirement(
        PhaseCardRequirementTemplate value,
        IReadOnlyDictionary<string, string> phaseIdsBySource,
        IReadOnlyDictionary<string, string> roleIdsBySource,
        IReadOnlyDictionary<string, string> itemIdsBySource,
        IReadOnlyDictionary<string, string> locationIdsBySource)
    {
        var childCardId = value.ChildCardType switch
        {
            StoryCardChildCardType.Role => roleIdsBySource.GetValueOrDefault(value.ChildCardId),
            StoryCardChildCardType.Item => itemIdsBySource.GetValueOrDefault(value.ChildCardId),
            StoryCardChildCardType.Location => locationIdsBySource.GetValueOrDefault(value.ChildCardId),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(childCardId))
            return null;

        return new()
        {
            Id = $"requirement-{Guid.NewGuid():N}",
            SourceTemplateChildId = value.Id,
            PhaseId = phaseIdsBySource[value.PhaseId],
            ChildCardType = value.ChildCardType,
            ChildCardId = childCardId,
            RequiredCount = Math.Max(1, value.RequiredCount),
            SortOrder = value.SortOrder
        };
    }

    static RoleCardInstance CloneRole(RoleCardTemplate value) => new()
    {
        Id = $"role-{Guid.NewGuid():N}",
        SourceTemplateChildId = value.Id,
        Title = value.Title,
        SelectionInstructions = value.SelectionInstructions,
        CreationInstructions = value.CreationInstructions,
        OngoingContext = value.OngoingContext,
        PrivateContext = value.PrivateContext,
        SortOrder = value.SortOrder
    };

    static ItemCardInstance CloneItem(ItemCardTemplate value) => new()
    {
        Id = $"item-card-{Guid.NewGuid():N}",
        SourceTemplateChildId = value.Id,
        Title = value.Title,
        SelectionInstructions = value.SelectionInstructions,
        CreationInstructions = value.CreationInstructions,
        OngoingContext = value.OngoingContext,
        SortOrder = value.SortOrder
    };

    static LocationCardInstance CloneLocation(LocationCardTemplate value) => new()
    {
        Id = $"location-card-{Guid.NewGuid():N}",
        SourceTemplateChildId = value.Id,
        Title = value.Title,
        SelectionInstructions = value.SelectionInstructions,
        CreationInstructions = value.CreationInstructions,
        OngoingContext = value.OngoingContext,
        SortOrder = value.SortOrder
    };

    static StoryCardChildCardType ParseChildCardType(string value) =>
        Enum.TryParse<StoryCardChildCardType>(value, out var type) ? type : StoryCardChildCardType.Role;
}
