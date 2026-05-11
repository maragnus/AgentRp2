using AgentRp.Data;
using AgentRp.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

internal static class StoryCardInstancePersistenceStore
{
    public static async Task<List<StoryCardInstance>> LoadAsync(RpDbContext dbContext, string chatId, CancellationToken cancellationToken)
    {
        var instances = await dbContext.StoryCardInstances.AsNoTracking()
            .Where(row => row.ChatId == chatId)
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        if (instances.Count == 0)
            return [];

        var instanceIds = instances.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var phases = await dbContext.StoryCardInstancePhases.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var transitions = await dbContext.StoryCardInstancePhaseTransitions.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var requirements = await dbContext.StoryCardInstancePhaseRequirements.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var roles = await dbContext.StoryCardInstanceRoles.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var items = await dbContext.StoryCardInstanceItems.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var locations = await dbContext.StoryCardInstanceLocations.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var assignments = await dbContext.StoryCardInstanceAssignments.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var history = await dbContext.StoryCardHistory.AsNoTracking()
            .Where(row => row.ChatId == chatId && instanceIds.Contains(row.StoryCardInstanceId))
            .OrderByDescending(row => row.CreatedUtc)
            .ToListAsync(cancellationToken);

        return instances.Select(instance => StoryCardPersistenceMapper.ToInstance(
                instance,
                phases.Where(row => row.StoryCardInstanceId == instance.Id),
                transitions.Where(row => row.StoryCardInstanceId == instance.Id),
                requirements.Where(row => row.StoryCardInstanceId == instance.Id),
                roles.Where(row => row.StoryCardInstanceId == instance.Id),
                items.Where(row => row.StoryCardInstanceId == instance.Id),
                locations.Where(row => row.StoryCardInstanceId == instance.Id),
                assignments.Where(row => row.StoryCardInstanceId == instance.Id),
                history.Where(row => row.StoryCardInstanceId == instance.Id)))
            .ToList();
    }

    public static async Task SaveAsync(RpDbContext dbContext, RpChatDocument document, DateTime now, CancellationToken cancellationToken)
    {
        var chatId = document.Chat.Id;
        var existing = await dbContext.StoryCardInstances
            .Where(row => row.ChatId == chatId)
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var desiredIds = document.StoryCards.Select(card => card.Id).ToHashSet(StringComparer.Ordinal);
        dbContext.StoryCardInstances.RemoveRange(existing.Values.Where(row => !desiredIds.Contains(row.Id)));

        for (var index = 0; index < document.StoryCards.Count; index++)
        {
            var card = document.StoryCards[index];
            card.ChatId = chatId;
            NormalizeInstance(card);
            if (!existing.TryGetValue(card.Id, out var row))
            {
                row = new()
                {
                    ChatId = chatId,
                    Id = card.Id,
                    CreatedUtc = card.CreatedUtc == default ? now : card.CreatedUtc
                };
                dbContext.StoryCardInstances.Add(row);
            }
            StoryCardPersistenceMapper.Apply(card, row, index, now);
            await SaveChildrenAsync(dbContext, chatId, card, cancellationToken);
        }
    }

    static async Task SaveChildrenAsync(RpDbContext dbContext, string chatId, StoryCardInstance card, CancellationToken cancellationToken)
    {
        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardInstancePhases,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.Phases,
            (model, row, index) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.SourceTemplateChildId = model.SourceTemplateChildId;
                row.Title = model.Title;
                row.SetupInstructions = model.SetupInstructions;
                row.PlanningContext = model.PlanningContext;
                row.EndCondition = model.EndCondition;
                row.IsOptional = model.IsOptional;
                row.IsEnding = model.IsEnding;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardInstancePhaseTransitions,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.PhaseTransitions,
            (model, row, index) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.FromPhaseId = model.FromPhaseId;
                row.ToPhaseId = model.ToPhaseId;
                row.ConditionInstructions = model.ConditionInstructions;
                row.IsDefault = model.IsDefault;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardInstancePhaseRequirements,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.PhaseRequirements,
            (model, row, index) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.SourceTemplateChildId = model.SourceTemplateChildId;
                row.PhaseId = model.PhaseId;
                row.ChildCardType = model.ChildCardType.ToString();
                row.ChildCardId = model.ChildCardId;
                row.RequiredCount = Math.Max(1, model.RequiredCount);
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardInstanceRoles,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.Roles,
            (model, row, index) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.SourceTemplateChildId = model.SourceTemplateChildId;
                row.Title = model.Title;
                row.SelectionInstructions = model.SelectionInstructions;
                row.CreationInstructions = model.CreationInstructions;
                row.OngoingContext = model.OngoingContext;
                row.PrivateContext = model.PrivateContext;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardInstanceItems,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.Items,
            (model, row, index) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.SourceTemplateChildId = model.SourceTemplateChildId;
                row.Title = model.Title;
                row.SelectionInstructions = model.SelectionInstructions;
                row.CreationInstructions = model.CreationInstructions;
                row.OngoingContext = model.OngoingContext;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardInstanceLocations,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.Locations,
            (model, row, index) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.SourceTemplateChildId = model.SourceTemplateChildId;
                row.Title = model.Title;
                row.SelectionInstructions = model.SelectionInstructions;
                row.CreationInstructions = model.CreationInstructions;
                row.OngoingContext = model.OngoingContext;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardInstanceAssignments,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.Assignments,
            (model, row, index) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.ChildCardType = model.ChildCardType.ToString();
                row.ChildCardId = model.ChildCardId;
                row.EntityId = model.EntityId;
                row.EntityName = model.EntityName;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardHistory,
            row => row.ChatId == chatId && row.StoryCardInstanceId == card.Id,
            card.History,
            (model, row, _) =>
            {
                row.ChatId = chatId;
                row.StoryCardInstanceId = card.Id;
                row.Id = model.Id;
                row.Kind = model.Kind.ToString();
                row.Title = model.Title;
                row.Details = model.Details;
                row.TurnNumber = model.TurnNumber;
                row.CreatedUtc = model.CreatedUtc;
            },
            cancellationToken);
    }

    static void NormalizeInstance(StoryCardInstance card)
    {
        var phaseIds = card.Phases.Select(phase => phase.Id).ToHashSet(StringComparer.Ordinal);
        var roleIds = card.Roles.Select(role => role.Id).ToHashSet(StringComparer.Ordinal);
        var itemIds = card.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var locationIds = card.Locations.Select(location => location.Id).ToHashSet(StringComparer.Ordinal);
        var seenRequirements = new HashSet<string>(StringComparer.Ordinal);
        var normalizedRequirements = new List<PhaseCardRequirementInstance>();

        foreach (var requirement in card.PhaseRequirements.OrderBy(requirement => requirement.SortOrder))
        {
            if (!phaseIds.Contains(requirement.PhaseId) || !ChildExists(requirement.ChildCardType, requirement.ChildCardId, roleIds, itemIds, locationIds))
                continue;

            var key = $"{requirement.PhaseId}|{requirement.ChildCardType}|{requirement.ChildCardId}";
            if (!seenRequirements.Add(key))
                continue;

            requirement.RequiredCount = Math.Max(1, requirement.RequiredCount);
            requirement.SortOrder = normalizedRequirements.Count;
            normalizedRequirements.Add(requirement);
        }
        card.PhaseRequirements = normalizedRequirements;

        var normalizedAssignments = new List<StoryCardEntityAssignment>();
        foreach (var assignment in card.Assignments.OrderBy(assignment => assignment.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(assignment.EntityId) || !ChildExists(assignment.ChildCardType, assignment.ChildCardId, roleIds, itemIds, locationIds))
                continue;

            assignment.SortOrder = normalizedAssignments.Count;
            normalizedAssignments.Add(assignment);
        }
        card.Assignments = normalizedAssignments;
    }

    static bool ChildExists(
        StoryCardChildCardType type,
        string childCardId,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> itemIds,
        IReadOnlySet<string> locationIds) => type switch
    {
        StoryCardChildCardType.Role => roleIds.Contains(childCardId),
        StoryCardChildCardType.Item => itemIds.Contains(childCardId),
        StoryCardChildCardType.Location => locationIds.Contains(childCardId),
        _ => false
    };
}
