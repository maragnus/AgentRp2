using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Session;
using AgentRp.UserSystem;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public interface IStoryCardCatalogService
{
    Task<IReadOnlyList<StoryCardTemplate>> LoadCatalogAsync(CurrentAppUser user, CancellationToken cancellationToken = default);
    Task<StoryCardTemplate?> LoadTemplateAsync(CurrentAppUser user, string templateId, bool lineageView = false, CancellationToken cancellationToken = default);
    Task<StoryCardTemplateDetails?> LoadTemplateDetailsAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default);
    Task<StoryCardTemplate> SaveTemplateAsync(CurrentAppUser user, StoryCardTemplate template, CancellationToken cancellationToken = default);
    Task<StoryCardTemplate> RemixAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default);
    Task ArchiveAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default);
    Task<StoryCardTemplate> RefreshStatsAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default);
    Task<StoryCardInstance> CreateInstanceAsync(CurrentAppUser user, string chatId, string templateId, int startTurnNumber, bool injected, CancellationToken cancellationToken = default);
}

public sealed class StoryCardCatalogService(IDbContextFactory<RpDbContext> dbContextFactory) : IStoryCardCatalogService
{
    public async Task<IReadOnlyList<StoryCardTemplate>> LoadCatalogAsync(CurrentAppUser user, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.StoryCardTemplates.AsNoTracking()
            .Where(row => row.RetiredUtc == null && (row.OwnerUserId == user.Id || row.IsShared || user.IsAdmin))
            .OrderByDescending(row => row.UpdatedUtc)
            .ToListAsync(cancellationToken);
        return await LoadTemplatesAsync(dbContext, rows, cancellationToken);
    }

    public async Task<StoryCardTemplate?> LoadTemplateAsync(CurrentAppUser user, string templateId, bool lineageView = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.StoryCardTemplates.AsNoTracking()
            .Where(row => row.Id == templateId)
            .OrderBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return null;
        if (!CanView(user, row, lineageView))
            return null;

        return (await LoadTemplatesAsync(dbContext, [row], cancellationToken)).FirstOrDefault();
    }

    public async Task<StoryCardTemplateDetails?> LoadTemplateDetailsAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.StoryCardTemplates.AsNoTracking()
            .Where(row => row.Id == templateId)
            .OrderBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null || !CanView(user, row, lineageView: false))
            return null;

        var parentId = row.ParentTemplateId;
        var rootId = string.IsNullOrWhiteSpace(row.RootTemplateId) ? row.Id : row.RootTemplateId;
        var relatedRows = await dbContext.StoryCardTemplates.AsNoTracking()
            .Where(candidate =>
                candidate.Id == row.Id ||
                (!string.IsNullOrEmpty(parentId) && candidate.Id == parentId) ||
                candidate.Id == rootId ||
                candidate.ParentTemplateId == row.Id ||
                candidate.RootTemplateId == rootId)
            .OrderByDescending(candidate => candidate.UpdatedUtc)
            .ToListAsync(cancellationToken);
        var visibleRows = relatedRows
            .Where(candidate => candidate.Id == row.Id || CanView(user, candidate, lineageView: candidate.Id == parentId || candidate.Id == rootId))
            .ToList();
        var templates = (await LoadTemplatesAsync(dbContext, visibleRows, cancellationToken))
            .ToDictionary(template => template.Id, StringComparer.Ordinal);

        if (!templates.TryGetValue(row.Id, out var template))
            return null;

        templates.TryGetValue(parentId, out var parent);
        templates.TryGetValue(rootId, out var root);
        var remixes = relatedRows
            .Where(candidate => candidate.Id != row.Id && candidate.ParentTemplateId == row.Id && CanView(user, candidate, lineageView: false))
            .Select(candidate => templates.GetValueOrDefault(candidate.Id))
            .Where(template => template is not null)
            .Select(template => template!)
            .OrderByDescending(template => template.UpdatedUtc)
            .ToList();

        return new()
        {
            Template = template,
            Parent = parent,
            Root = root?.Id == template.Id ? null : root,
            Remixes = remixes
        };
    }

    public async Task<StoryCardTemplate> SaveTemplateAsync(CurrentAppUser user, StoryCardTemplate template, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        StoryCardTemplateRow? existing = null;
        if (!string.IsNullOrWhiteSpace(template.Id))
        {
            existing = await dbContext.StoryCardTemplates
                .Where(row => row.Id == template.Id)
                .OrderBy(row => row.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (existing is not null && !CanEdit(user, existing))
            throw new UnauthorizedAccessException($"Saving story card '{template.Title}' failed because it belongs to another user.");

        if (existing is null)
        {
            template.Id = string.IsNullOrWhiteSpace(template.Id) ? $"story-card-template-{Guid.NewGuid():N}" : template.Id;
            template.OwnerUserId = user.Id;
            template.OwnerDisplayName = user.DisplayName;
            template.CreatedUtc = now;
            existing = new()
            {
                Id = template.Id,
                CreatedUtc = now
            };
            dbContext.StoryCardTemplates.Add(existing);
        }
        else
        {
            template.OwnerUserId = existing.OwnerUserId;
            template.OwnerDisplayName = existing.OwnerDisplayName;
            template.CreatedUtc = existing.CreatedUtc;
            template.TemplateVersion = existing.TemplateVersion + 1;
        }

        NormalizeTemplate(template, now);
        StoryCardPersistenceMapper.Apply(template, existing, now);
        await SaveChildrenAsync(dbContext, template, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadTemplateAsync(user, template.Id, lineageView: true, cancellationToken) ?? template;
    }

    public async Task<StoryCardTemplate> RemixAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default)
    {
        var source = await LoadTemplateAsync(user, templateId, lineageView: true, cancellationToken)
            ?? throw new InvalidOperationException("Remixing the story card failed because the source card was not found.");
        var now = DateTime.UtcNow;
        var remix = CloneTemplate(source);
        remix.Id = $"story-card-template-{Guid.NewGuid():N}";
        remix.OwnerUserId = user.Id;
        remix.OwnerDisplayName = user.DisplayName;
        remix.Title = $"Remix of {source.Title}";
        remix.IsShared = false;
        remix.RetiredUtc = null;
        remix.ParentTemplateId = source.Id;
        remix.RootTemplateId = string.IsNullOrWhiteSpace(source.RootTemplateId) ? source.Id : source.RootTemplateId;
        remix.TemplateVersion = 1;
        remix.Stats = new();
        remix.CreatedUtc = now;
        remix.UpdatedUtc = now;
        return await SaveTemplateAsync(user, remix, cancellationToken);
    }

    public async Task ArchiveAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.StoryCardTemplates
            .Where(row => row.Id == templateId)
            .OrderBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return;
        if (!CanEdit(user, row))
            throw new UnauthorizedAccessException("Archiving the story card failed because it belongs to another user.");

        row.RetiredUtc = DateTime.UtcNow;
        row.IsShared = false;
        row.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoryCardTemplate> RefreshStatsAsync(CurrentAppUser user, string templateId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.StoryCardTemplates
            .Where(row => row.Id == templateId)
            .OrderBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Refreshing story card stats failed because the card was not found.");
        if (!CanView(user, row, lineageView: true))
            throw new UnauthorizedAccessException("Refreshing story card stats failed because the card is not visible to this user.");

        var rootId = string.IsNullOrWhiteSpace(row.RootTemplateId) ? row.Id : row.RootTemplateId;
        var remixIds = await dbContext.StoryCardTemplates.AsNoTracking()
            .Where(candidate => candidate.Id != row.Id && (candidate.RootTemplateId == rootId || candidate.ParentTemplateId == row.Id))
            .Select(candidate => candidate.Id)
            .ToListAsync(cancellationToken);
        var directStats = await CalculateStatsAsync(dbContext, [row.Id], cancellationToken);
        var remixStats = await CalculateStatsAsync(dbContext, remixIds, cancellationToken);

        row.DirectStoryCount = directStats.StoryCount;
        row.DirectActiveTurnCount = directStats.ActiveTurnCount;
        row.RemixCount = remixIds.Count;
        row.RemixStoryCount = remixStats.StoryCount;
        row.RemixActiveTurnCount = remixStats.ActiveTurnCount;
        row.StatsRefreshedUtc = DateTime.UtcNow;
        row.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadTemplateAsync(user, templateId, lineageView: true, cancellationToken) ?? throw new InvalidOperationException("Reloading refreshed story card stats failed.");
    }

    public async Task<StoryCardInstance> CreateInstanceAsync(CurrentAppUser user, string chatId, string templateId, int startTurnNumber, bool injected, CancellationToken cancellationToken = default)
    {
        var template = await LoadTemplateAsync(user, templateId, lineageView: false, cancellationToken)
            ?? throw new InvalidOperationException("Adding the story card failed because the card was not found.");
        if (template.RetiredUtc is not null || (!template.IsShared && template.OwnerUserId != user.Id && !user.IsAdmin))
            throw new UnauthorizedAccessException("Adding the story card failed because it is not available for stories.");

        return StoryCardPersistenceMapper.CloneToInstance(template, chatId, startTurnNumber, injected);
    }

    static async Task<IReadOnlyList<StoryCardTemplate>> LoadTemplatesAsync(RpDbContext dbContext, IReadOnlyList<StoryCardTemplateRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var ids = rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var phases = await dbContext.StoryCardTemplatePhases.AsNoTracking()
            .Where(row => ids.Contains(row.StoryCardTemplateId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var transitions = await dbContext.StoryCardTemplatePhaseTransitions.AsNoTracking()
            .Where(row => ids.Contains(row.StoryCardTemplateId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var requirements = await dbContext.StoryCardTemplatePhaseRequirements.AsNoTracking()
            .Where(row => ids.Contains(row.StoryCardTemplateId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var roles = await dbContext.StoryCardTemplateRoles.AsNoTracking()
            .Where(row => ids.Contains(row.StoryCardTemplateId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var items = await dbContext.StoryCardTemplateItems.AsNoTracking()
            .Where(row => ids.Contains(row.StoryCardTemplateId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);
        var locations = await dbContext.StoryCardTemplateLocations.AsNoTracking()
            .Where(row => ids.Contains(row.StoryCardTemplateId))
            .OrderBy(row => row.SortOrder)
            .ToListAsync(cancellationToken);

        return rows.Select(row => StoryCardPersistenceMapper.ToTemplate(
                row,
                phases.Where(child => child.StoryCardTemplateId == row.Id),
                transitions.Where(child => child.StoryCardTemplateId == row.Id),
                requirements.Where(child => child.StoryCardTemplateId == row.Id),
                roles.Where(child => child.StoryCardTemplateId == row.Id),
                items.Where(child => child.StoryCardTemplateId == row.Id),
                locations.Where(child => child.StoryCardTemplateId == row.Id)))
            .ToList();
    }

    static async Task SaveChildrenAsync(RpDbContext dbContext, StoryCardTemplate template, CancellationToken cancellationToken)
    {
        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardTemplatePhases,
            row => row.StoryCardTemplateId == template.Id,
            template.Phases,
            (model, row, index) =>
            {
                row.StoryCardTemplateId = template.Id;
                row.Id = model.Id;
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
            dbContext.StoryCardTemplatePhaseTransitions,
            row => row.StoryCardTemplateId == template.Id,
            template.PhaseTransitions,
            (model, row, index) =>
            {
                row.StoryCardTemplateId = template.Id;
                row.Id = model.Id;
                row.FromPhaseId = model.FromPhaseId;
                row.ToPhaseId = model.ToPhaseId;
                row.ConditionInstructions = model.ConditionInstructions;
                row.IsDefault = model.IsDefault;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardTemplatePhaseRequirements,
            row => row.StoryCardTemplateId == template.Id,
            template.PhaseRequirements,
            (model, row, index) =>
            {
                row.StoryCardTemplateId = template.Id;
                row.Id = model.Id;
                row.PhaseId = model.PhaseId;
                row.ChildCardType = model.ChildCardType.ToString();
                row.ChildCardId = model.ChildCardId;
                row.RequiredCount = Math.Max(1, model.RequiredCount);
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardTemplateRoles,
            row => row.StoryCardTemplateId == template.Id,
            template.Roles,
            (model, row, index) =>
            {
                row.StoryCardTemplateId = template.Id;
                row.Id = model.Id;
                row.Title = model.Title;
                row.SelectionInstructions = model.SelectionInstructions;
                row.CreationInstructions = model.CreationInstructions;
                row.OngoingContext = model.OngoingContext;
                row.PrivateContext = model.PrivateContext;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardTemplateItems,
            row => row.StoryCardTemplateId == template.Id,
            template.Items,
            (model, row, index) =>
            {
                row.StoryCardTemplateId = template.Id;
                row.Id = model.Id;
                row.Title = model.Title;
                row.SelectionInstructions = model.SelectionInstructions;
                row.CreationInstructions = model.CreationInstructions;
                row.OngoingContext = model.OngoingContext;
                row.SortOrder = index;
            },
            cancellationToken);

        await StoryCardChildRowPersistence.SaveAsync(
            dbContext.StoryCardTemplateLocations,
            row => row.StoryCardTemplateId == template.Id,
            template.Locations,
            (model, row, index) =>
            {
                row.StoryCardTemplateId = template.Id;
                row.Id = model.Id;
                row.Title = model.Title;
                row.SelectionInstructions = model.SelectionInstructions;
                row.CreationInstructions = model.CreationInstructions;
                row.OngoingContext = model.OngoingContext;
                row.SortOrder = index;
            },
            cancellationToken);
    }

    static void NormalizeTemplate(StoryCardTemplate template, DateTime now)
    {
        template.Title = string.IsNullOrWhiteSpace(template.Title) ? "Untitled Story Card" : template.Title.Trim();
        template.Summary = template.Summary.Trim();
        template.Instructions = template.Instructions.Trim();
        template.UpdatedUtc = now;
        template.RootTemplateId = string.IsNullOrWhiteSpace(template.RootTemplateId) ? "" : template.RootTemplateId;
        EnsureIds(template.Phases, "phase");
        EnsureIds(template.Roles, "role");
        EnsureIds(template.Items, "item-card");
        EnsureIds(template.Locations, "location-card");
        EnsureIds(template.PhaseRequirements, "requirement");
        NormalizeRequirements(template);
        StoryCardPersistenceMapper.EnsureLinearTransitions(template);
    }

    static void NormalizeRequirements(StoryCardTemplate template)
    {
        var phaseIds = template.Phases.Select(phase => phase.Id).ToHashSet(StringComparer.Ordinal);
        var roleIds = template.Roles.Select(role => role.Id).ToHashSet(StringComparer.Ordinal);
        var itemIds = template.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var locationIds = template.Locations.Select(location => location.Id).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<PhaseCardRequirementTemplate>();

        foreach (var requirement in template.PhaseRequirements.OrderBy(requirement => requirement.SortOrder))
        {
            if (!phaseIds.Contains(requirement.PhaseId) || !ChildExists(requirement, roleIds, itemIds, locationIds))
                continue;

            var key = $"{requirement.PhaseId}|{requirement.ChildCardType}|{requirement.ChildCardId}";
            if (!seen.Add(key))
                continue;

            requirement.RequiredCount = Math.Max(1, requirement.RequiredCount);
            requirement.SortOrder = normalized.Count;
            normalized.Add(requirement);
        }

        template.PhaseRequirements = normalized;
    }

    static bool ChildExists(
        PhaseCardRequirementTemplate requirement,
        IReadOnlySet<string> roleIds,
        IReadOnlySet<string> itemIds,
        IReadOnlySet<string> locationIds) => requirement.ChildCardType switch
    {
        StoryCardChildCardType.Role => roleIds.Contains(requirement.ChildCardId),
        StoryCardChildCardType.Item => itemIds.Contains(requirement.ChildCardId),
        StoryCardChildCardType.Location => locationIds.Contains(requirement.ChildCardId),
        _ => false
    };

    static void EnsureIds<TModel>(IReadOnlyList<TModel> models, string prefix)
        where TModel : IStoryCardChildModel
    {
        for (var index = 0; index < models.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(models[index].Id))
                continue;

            models[index].Id = $"{prefix}-{Guid.NewGuid():N}";
        }
    }

    static StoryCardTemplate CloneTemplate(StoryCardTemplate source)
    {
        var clone = new StoryCardTemplate
        {
            Summary = source.Summary,
            Instructions = source.Instructions
        };
        var phaseIdsBySource = new Dictionary<string, string>(StringComparer.Ordinal);
        var roleIdsBySource = new Dictionary<string, string>(StringComparer.Ordinal);
        var itemIdsBySource = new Dictionary<string, string>(StringComparer.Ordinal);
        var locationIdsBySource = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var phase in source.Phases)
        {
            var clonePhase = new PhaseCardTemplate
            {
                Id = $"phase-{Guid.NewGuid():N}",
                Title = phase.Title,
                SetupInstructions = phase.SetupInstructions,
                PlanningContext = phase.PlanningContext,
                EndCondition = phase.EndCondition,
                IsOptional = phase.IsOptional,
                IsEnding = phase.IsEnding,
                SortOrder = phase.SortOrder
            };
            clone.Phases.Add(clonePhase);
            phaseIdsBySource[phase.Id] = clonePhase.Id;
        }

        foreach (var role in source.Roles)
        {
            var cloneRole = new RoleCardTemplate
            {
                Id = $"role-{Guid.NewGuid():N}",
                Title = role.Title,
                SelectionInstructions = role.SelectionInstructions,
                CreationInstructions = role.CreationInstructions,
                OngoingContext = role.OngoingContext,
                PrivateContext = role.PrivateContext,
                SortOrder = role.SortOrder
            };
            clone.Roles.Add(cloneRole);
            roleIdsBySource[role.Id] = cloneRole.Id;
        }

        foreach (var item in source.Items)
        {
            var cloneItem = new ItemCardTemplate
            {
                Id = $"item-card-{Guid.NewGuid():N}",
                Title = item.Title,
                SelectionInstructions = item.SelectionInstructions,
                CreationInstructions = item.CreationInstructions,
                OngoingContext = item.OngoingContext,
                SortOrder = item.SortOrder
            };
            clone.Items.Add(cloneItem);
            itemIdsBySource[item.Id] = cloneItem.Id;
        }

        foreach (var location in source.Locations)
        {
            var cloneLocation = new LocationCardTemplate
            {
                Id = $"location-card-{Guid.NewGuid():N}",
                Title = location.Title,
                SelectionInstructions = location.SelectionInstructions,
                CreationInstructions = location.CreationInstructions,
                OngoingContext = location.OngoingContext,
                SortOrder = location.SortOrder
            };
            clone.Locations.Add(cloneLocation);
            locationIdsBySource[location.Id] = cloneLocation.Id;
        }

        clone.PhaseRequirements = source.PhaseRequirements
            .Select(requirement => CloneRequirement(requirement, phaseIdsBySource, roleIdsBySource, itemIdsBySource, locationIdsBySource))
            .Where(requirement => requirement is not null)
            .Select(requirement => requirement!)
            .ToList();
        return clone;
    }

    static PhaseCardRequirementTemplate? CloneRequirement(
        PhaseCardRequirementTemplate source,
        IReadOnlyDictionary<string, string> phaseIdsBySource,
        IReadOnlyDictionary<string, string> roleIdsBySource,
        IReadOnlyDictionary<string, string> itemIdsBySource,
        IReadOnlyDictionary<string, string> locationIdsBySource)
    {
        if (!phaseIdsBySource.TryGetValue(source.PhaseId, out var phaseId))
            return null;

        var childCardId = source.ChildCardType switch
        {
            StoryCardChildCardType.Role => roleIdsBySource.GetValueOrDefault(source.ChildCardId),
            StoryCardChildCardType.Item => itemIdsBySource.GetValueOrDefault(source.ChildCardId),
            StoryCardChildCardType.Location => locationIdsBySource.GetValueOrDefault(source.ChildCardId),
            _ => null
        };
        if (string.IsNullOrWhiteSpace(childCardId))
            return null;

        return new()
        {
            Id = $"requirement-{Guid.NewGuid():N}",
            PhaseId = phaseId,
            ChildCardType = source.ChildCardType,
            ChildCardId = childCardId,
            RequiredCount = Math.Max(1, source.RequiredCount),
            SortOrder = source.SortOrder
        };
    }

    static bool CanView(CurrentAppUser user, StoryCardTemplateRow row, bool lineageView) =>
        user.IsAdmin || row.OwnerUserId == user.Id || row.IsShared || lineageView;

    static bool CanEdit(CurrentAppUser user, StoryCardTemplateRow row) =>
        user.IsAdmin || row.OwnerUserId == user.Id;

    static async Task<(int StoryCount, int ActiveTurnCount)> CalculateStatsAsync(RpDbContext dbContext, IReadOnlyList<string> templateIds, CancellationToken cancellationToken)
    {
        if (templateIds.Count == 0)
            return (0, 0);

        var rows = await (
            from instance in dbContext.StoryCardInstances.AsNoTracking()
            join chat in dbContext.Chats.AsNoTracking() on instance.ChatId equals chat.Id
            where templateIds.Contains(instance.SourceTemplateId)
            select new
            {
                instance.ChatId,
                instance.StartTurnNumber,
                instance.EndTurnNumber,
                chat.LastGeneratedTurnNumber
            }).ToListAsync(cancellationToken);
        var storyCount = rows.Select(row => row.ChatId).Distinct(StringComparer.Ordinal).Count();
        var turnCount = rows.Sum(row => Math.Max(0, (row.EndTurnNumber ?? row.LastGeneratedTurnNumber) - row.StartTurnNumber + 1));
        return (storyCount, turnCount);
    }
}
