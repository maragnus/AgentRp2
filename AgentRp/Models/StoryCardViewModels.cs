namespace AgentRp.Models;

public enum StoryCardViewKind
{
    Template,
    Instance
}

public sealed class StoryCardViewModel
{
    public const string CoverImageUrl = "/img/story-card-placeholder.avif";

    public string Id { get; init; } = "";
    public StoryCardViewKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string CoverUrl { get; init; } = CoverImageUrl;
    public int PhaseCount { get; init; }
    public int RoleCount { get; init; }
    public int LocationCount { get; init; }
    public int ItemCount { get; init; }
    public int UsageCount { get; init; }
    public int? AverageTurns { get; init; }
    public string StatusText { get; init; } = "";
    public string StatusTone { get; init; } = "blue";
    public string TurnRangeText { get; init; } = "";
    public bool IsShared { get; init; }
    public bool IsRetired { get; init; }

    public static StoryCardViewModel FromTemplate(StoryCardTemplate template) => new()
    {
        Id = template.Id,
        Kind = StoryCardViewKind.Template,
        Title = template.Title,
        Summary = SummaryOrCounts(template.Summary, template.Phases.Count, template.Roles.Count, template.Locations.Count, template.Items.Count),
        PhaseCount = template.Phases.Count,
        RoleCount = template.Roles.Count,
        LocationCount = template.Locations.Count,
        ItemCount = template.Items.Count,
        UsageCount = template.Stats.DirectStoryCount,
        AverageTurns = template.Stats.DirectStoryCount > 0
            ? (int)Math.Round((double)template.Stats.DirectActiveTurnCount / template.Stats.DirectStoryCount, MidpointRounding.AwayFromZero)
            : null,
        IsShared = template.IsShared,
        IsRetired = template.RetiredUtc is not null
    };

    public static StoryCardViewModel FromInstance(StoryCardInstance instance, int currentTurnNumber) => new()
    {
        Id = instance.Id,
        Kind = StoryCardViewKind.Instance,
        Title = instance.Title,
        Summary = SummaryOrCounts(instance.Summary, instance.Phases.Count, instance.Roles.Count, instance.Locations.Count, instance.Items.Count),
        PhaseCount = instance.Phases.Count,
        RoleCount = instance.Roles.Count,
        LocationCount = instance.Locations.Count,
        ItemCount = instance.Items.Count,
        StatusText = instance.Status.ToString(),
        StatusTone = ToneForStatus(instance.Status),
        TurnRangeText = TurnRange(instance, currentTurnNumber)
    };

    static string SummaryOrCounts(string summary, int phaseCount, int roleCount, int locationCount, int itemCount)
    {
        if (!string.IsNullOrWhiteSpace(summary))
            return summary;

        var childCount = phaseCount + roleCount + locationCount + itemCount;
        return childCount == 1 ? "1 child card" : $"{childCount} child cards";
    }

    static string TurnRange(StoryCardInstance instance, int currentTurnNumber)
    {
        var end = instance.EndTurnNumber ?? currentTurnNumber;
        return $"Turns {instance.StartTurnNumber} to {end}";
    }

    public static string ToneForStatus(StoryCardStatus status) => status switch
    {
        StoryCardStatus.Dormant => "amber",
        StoryCardStatus.Concluded => "emerald",
        _ => "blue"
    };
}

public sealed class StoryCardTemplateDetails
{
    public StoryCardTemplate Template { get; init; } = new();
    public StoryCardTemplate? Parent { get; init; }
    public StoryCardTemplate? Root { get; init; }
    public IReadOnlyList<StoryCardTemplate> Remixes { get; init; } = [];
}
