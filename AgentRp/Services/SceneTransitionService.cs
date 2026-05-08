using System.Text;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public enum SceneNarratorGuidancePurpose
{
    OpeningScene,
    LocationTransition,
    TimeSkip,
    SceneReset
}

public sealed record SceneNarratorGuidance(
    SceneNarratorGuidancePurpose Purpose,
    string Guidance);

public sealed record SetSceneRequest(
    string LocationId,
    IReadOnlyList<string> CharacterIds,
    IReadOnlyList<string> ItemIds,
    SceneNarratorGuidance NarratorGuidance);

public sealed record SceneTransitionEntity(string Id, string Name);

public enum SceneTransitionLineKind
{
    CharactersLeft,
    CharactersEntered,
    ItemsRemoved,
    ItemsAdded,
    LocationChanged,
    CharactersPresent,
    ItemsPresent
}

public sealed record SceneTransitionMention(string EntityType, string Id, string Name);

public sealed record SceneTransitionLine(
    SceneTransitionLineKind Kind,
    IReadOnlyList<SceneTransitionMention> Entities,
    SceneTransitionMention? CurrentLocation = null,
    SceneTransitionMention? PreviousLocation = null);

public sealed record SceneTransitionDelta(
    bool IsLocationTransition,
    IReadOnlyList<SceneTransitionLine> Lines)
{
    public bool HasChanges => Lines.Count > 0;
}

public sealed record SceneTransitionPlan(
    RpSceneFrame PreviousScene,
    RpSceneFrame TargetScene,
    bool IsOpeningScene,
    bool IsLocationTransition,
    bool IsTimeSkip,
    bool IsSceneReset,
    IReadOnlyList<SceneTransitionEntity> AddedCharacters,
    IReadOnlyList<SceneTransitionEntity> RemovedCharacters,
    IReadOnlyList<SceneTransitionEntity> AddedItems,
    IReadOnlyList<SceneTransitionEntity> RemovedItems,
    string NarratorInstruction);

public sealed record SceneTransitionResult(
    SceneTransitionPlan Plan,
    string NarratorTurnId,
    string NarratorMessage);

public sealed class SceneTransitionService
{
    public SceneTransitionDelta BuildDelta(RpChatDocument document, RpSceneFrame previous, RpSceneFrame target)
    {
        var isLocationTransition = !string.Equals(previous.LocationId, target.LocationId, StringComparison.Ordinal);
        var removedCharacters = Mentions(
            EntityTypes.Character,
            previous.InSceneCharacterIds.Where(id => !target.InSceneCharacterIds.Contains(id, StringComparer.Ordinal)),
            document.Characters.Select(character => (character.Id, character.Name)));
        var addedCharacters = Mentions(
            EntityTypes.Character,
            target.InSceneCharacterIds.Where(id => !previous.InSceneCharacterIds.Contains(id, StringComparer.Ordinal)),
            document.Characters.Select(character => (character.Id, character.Name)));
        var removedItems = Mentions(
            EntityTypes.Item,
            previous.InSceneItemIds.Where(id => !target.InSceneItemIds.Contains(id, StringComparer.Ordinal)),
            document.Items.Select(item => (item.Id, item.Name)));
        var addedItems = Mentions(
            EntityTypes.Item,
            target.InSceneItemIds.Where(id => !previous.InSceneItemIds.Contains(id, StringComparer.Ordinal)),
            document.Items.Select(item => (item.Id, item.Name)));
        var presentCharacters = Mentions(
            EntityTypes.Character,
            target.InSceneCharacterIds,
            document.Characters.Select(character => (character.Id, character.Name)));
        var presentItems = Mentions(
            EntityTypes.Item,
            target.InSceneItemIds,
            document.Items.Select(item => (item.Id, item.Name)));

        var lines = new List<SceneTransitionLine>();
        AddLine(lines, SceneTransitionLineKind.CharactersLeft, removedCharacters);
        AddLine(lines, SceneTransitionLineKind.ItemsRemoved, removedItems);
        if (isLocationTransition)
        {
            var currentLocation = ResolveLocationMention(document, target);
            if (currentLocation is not null)
            {
                lines.Add(new(
                    SceneTransitionLineKind.LocationChanged,
                    [],
                    currentLocation,
                    ResolveLocationMention(document, previous)));
            }

            AddLine(lines, SceneTransitionLineKind.CharactersPresent, presentCharacters);
            AddLine(lines, SceneTransitionLineKind.ItemsPresent, presentItems);
        }
        else
        {
            AddLine(lines, SceneTransitionLineKind.CharactersEntered, addedCharacters);
            AddLine(lines, SceneTransitionLineKind.ItemsAdded, addedItems);
        }

        return new(isLocationTransition, lines);
    }

    public string FormatForTranscript(SceneTransitionDelta delta)
    {
        var lines = delta.Lines
            .Select(FormatLine)
            .Where(line => !string.IsNullOrWhiteSpace(line));
        return string.Join(Environment.NewLine, lines);
    }

    public SceneTransitionPlan Build(RpChatDocument document, SetSceneRequest request)
    {
        ValidateNarratorGuidance(request.NarratorGuidance);
        var location = document.Locations.FirstOrDefault(item => item.Id == request.LocationId)
            ?? throw new SceneTransitionValidationException($"Setting the scene failed because no location with id '{request.LocationId}' exists.");
        var characterIds = DistinctIds(request.CharacterIds);
        var itemIds = DistinctIds(request.ItemIds);
        ValidateIds(characterIds, document.Characters.Select(item => item.Id), "character");
        ValidateIds(itemIds, document.Items.Select(item => item.Id), "item");

        var previous = SessionCloner.Clone(TranscriptGraph.GetActiveScene(document.Transcript));
        var target = new RpSceneFrame
        {
            LocationId = location.Id,
            LocationName = location.Name,
            InSceneCharacterIds = characterIds,
            InSceneItemIds = itemIds
        };
        var isOpening = TranscriptGraph.GetActivePath(document.Transcript).Count == 0;
        var locationChanged = !string.Equals(previous.LocationId, target.LocationId, StringComparison.Ordinal);
        var isLocationTransition = !isOpening && locationChanged;
        var isTimeSkip = request.NarratorGuidance.Purpose == SceneNarratorGuidancePurpose.TimeSkip;
        var isSceneReset = request.NarratorGuidance.Purpose == SceneNarratorGuidancePurpose.SceneReset;
        var delta = BuildDelta(document, previous, target);
        var addedCharacters = Added(target.InSceneCharacterIds, previous.InSceneCharacterIds, document.Characters);
        var removedCharacters = Removed(previous.InSceneCharacterIds, target.InSceneCharacterIds, document.Characters);
        var addedItems = Added(target.InSceneItemIds, previous.InSceneItemIds, document.Items);
        var removedItems = Removed(previous.InSceneItemIds, target.InSceneItemIds, document.Items);
        var instruction = BuildNarratorInstruction(
            document,
            previous,
            target,
            isOpening,
            isLocationTransition,
            request,
            delta);

        return new(
            previous,
            target,
            isOpening,
            isLocationTransition,
            isTimeSkip,
            isSceneReset,
            addedCharacters,
            removedCharacters,
            addedItems,
            removedItems,
            instruction);
    }

    static List<string> DistinctIds(IReadOnlyList<string> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    static void AddLine(List<SceneTransitionLine> lines, SceneTransitionLineKind kind, IReadOnlyList<SceneTransitionMention> entities)
    {
        if (entities.Count > 0)
            lines.Add(new(kind, entities));
    }

    static List<SceneTransitionMention> Mentions(
        string entityType,
        IEnumerable<string> ids,
        IEnumerable<(string Id, string Name)> entities)
    {
        var byId = entities.ToDictionary(entity => entity.Id, entity => entity.Name, StringComparer.Ordinal);
        return ids
            .Where(id => byId.ContainsKey(id))
            .Select(id => new SceneTransitionMention(entityType, id, byId[id]))
            .ToList();
    }

    static SceneTransitionMention? ResolveLocationMention(RpChatDocument document, RpSceneFrame scene)
    {
        var location = document.Locations.FirstOrDefault(location => string.Equals(location.Id, scene.LocationId, StringComparison.Ordinal));
        var name = location?.Name ?? scene.LocationName;
        if (string.IsNullOrWhiteSpace(location?.Id ?? scene.LocationId) && IsMissingLocationName(name))
            return null;

        return new(EntityTypes.Location, location?.Id ?? scene.LocationId, string.IsNullOrWhiteSpace(name) ? "Unknown location" : name);
    }

    static bool IsMissingLocationName(string? name) =>
        string.IsNullOrWhiteSpace(name)
        || string.Equals(name.Trim(), "No Location", StringComparison.OrdinalIgnoreCase);

    static string FormatLine(SceneTransitionLine line) => line.Kind switch
    {
        SceneTransitionLineKind.CharactersLeft => $"{FormatMentionList(line.Entities)} left the scene.",
        SceneTransitionLineKind.CharactersEntered => $"{FormatMentionList(line.Entities)} entered the scene.",
        SceneTransitionLineKind.ItemsRemoved => $"{FormatMentionList(line.Entities)} {WasWere(line.Entities)} removed from the scene.",
        SceneTransitionLineKind.ItemsAdded => $"{FormatMentionList(line.Entities)} {WasWere(line.Entities)} added to the scene.",
        SceneTransitionLineKind.LocationChanged => FormatLocationLine(line),
        SceneTransitionLineKind.CharactersPresent => $"{FormatMentionList(line.Entities)} {IsAre(line.Entities)} present in the scene.",
        SceneTransitionLineKind.ItemsPresent => $"{FormatMentionList(line.Entities)} {IsAre(line.Entities)} present in the scene.",
        _ => ""
    };

    static string FormatLocationLine(SceneTransitionLine line)
    {
        var current = line.CurrentLocation?.Name;
        var previous = line.PreviousLocation?.Name;
        if (string.IsNullOrWhiteSpace(current))
            return "";

        return string.IsNullOrWhiteSpace(previous)
            ? $"{current}."
            : $"{current} (previously {previous}).";
    }

    static string FormatMentionList(IReadOnlyList<SceneTransitionMention> mentions) =>
        FormatNameList(mentions.Select(mention => mention.Name).ToList());

    static string FormatNameList(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "",
        1 => names[0],
        2 => $"{names[0]} and {names[1]}",
        _ => $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}"
    };

    static string WasWere(IReadOnlyCollection<SceneTransitionMention> mentions) =>
        mentions.Count == 1 ? "was" : "were";

    static string IsAre(IReadOnlyCollection<SceneTransitionMention> mentions) =>
        mentions.Count == 1 ? "is" : "are";

    static void ValidateIds(IEnumerable<string> requestedIds, IEnumerable<string> existingIds, string entityName)
    {
        var existing = existingIds.ToHashSet(StringComparer.Ordinal);
        var missing = requestedIds.Where(id => !existing.Contains(id)).ToList();
        if (missing.Count == 0)
            return;

        throw new SceneTransitionValidationException($"Setting the scene failed because these {entityName} ids do not exist: {string.Join(", ", missing)}.");
    }

    static List<SceneTransitionEntity> Added<T>(IEnumerable<string> targetIds, IEnumerable<string> previousIds, IEnumerable<T> entities)
        where T : class =>
        Difference(targetIds, previousIds, entities);

    static List<SceneTransitionEntity> Removed<T>(IEnumerable<string> previousIds, IEnumerable<string> targetIds, IEnumerable<T> entities)
        where T : class =>
        Difference(previousIds, targetIds, entities);

    static List<SceneTransitionEntity> Difference<T>(IEnumerable<string> sourceIds, IEnumerable<string> excludeIds, IEnumerable<T> entities)
        where T : class
    {
        var excluded = excludeIds.ToHashSet(StringComparer.Ordinal);
        return ResolveEntities(sourceIds.Where(id => !excluded.Contains(id)), entities);
    }

    static List<SceneTransitionEntity> ResolveEntities<T>(IEnumerable<string> ids, IEnumerable<T> entities)
        where T : class
    {
        var byId = entities.ToDictionary(EntityId, EntityName, StringComparer.Ordinal);
        return ids.Where(byId.ContainsKey).Select(id => new SceneTransitionEntity(id, byId[id])).ToList();
    }

    static string EntityId<T>(T entity) where T : class => entity switch
    {
        RpCharacter character => character.Id,
        RpItem item => item.Id,
        _ => ""
    };

    static string EntityName<T>(T entity) where T : class => entity switch
    {
        RpCharacter character => character.Name,
        RpItem item => item.Name,
        _ => ""
    };

    string BuildNarratorInstruction(
        RpChatDocument document,
        RpSceneFrame previous,
        RpSceneFrame target,
        bool isOpening,
        bool isLocationTransition,
        SetSceneRequest request,
        SceneTransitionDelta delta)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Scene setting purpose: {FormatPurpose(request.NarratorGuidance.Purpose)}.");
        builder.AppendLine($"Narrator guidance: {request.NarratorGuidance.Guidance.Trim()}");
        builder.AppendLine("Stage the requested scene through the normal narrator pipeline.");
        builder.AppendLine("Focus on location, atmosphere, current mode, elapsed time, travel, mundane logistics, character positions, visible clothing/state, present items, and the transition into this moment.");
        builder.AppendLine("You may summarize already-established or user-approved offscreen continuity, and you may resolve low-stakes positioning needed to reach this scene.");
        builder.AppendLine("Do not create the next playable character beat: no dialogue, internal monologue, new emotional reactions, jokes, threats, confessions, reveals, attacks, or decisions for characters.");
        builder.AppendLine("End with the scene staged so a character can react next.");
        if (isOpening)
            builder.AppendLine("This is the opening scene.");
        else if (isLocationTransition)
            builder.AppendLine($"Note the transition to the new location: {target.LocationName}.");
        else
            builder.AppendLine("Refresh the current scene state without treating it as a major location change.");

        if (!isOpening && isLocationTransition)
            builder.AppendLine($"Previous location: {previous.LocationName}.");

        var transitionSummary = FormatForTranscript(delta);
        if (!string.IsNullOrWhiteSpace(transitionSummary))
        {
            builder.AppendLine("Scene delta:");
            builder.AppendLine(transitionSummary.Trim());
        }

        builder.AppendLine("Target scene:");
        builder.AppendLine($"Location: {target.LocationName}.");
        AppendList(builder, "Characters in scene", ResolveEntities(target.InSceneCharacterIds, document.Characters));
        AppendList(builder, "Items in scene", ResolveEntities(target.InSceneItemIds, document.Items));
        builder.AppendLine("Do not invent major off-screen consequences, relationship resolutions, losses, victories, or irreversible plot outcomes unless the user explicitly requested them.");
        builder.AppendLine("Write narration only, with no character speech, no meta commentary, and no tool/schema language.");
        return builder.ToString().Trim();
    }

    static void ValidateNarratorGuidance(SceneNarratorGuidance guidance)
    {
        if (!Enum.IsDefined(guidance.Purpose))
            throw new SceneTransitionValidationException("Setting the scene failed because narrator guidance has an unsupported purpose.");

        if (string.IsNullOrWhiteSpace(guidance.Guidance))
            throw new SceneTransitionValidationException("Setting the scene failed because narrator guidance is required.");
    }

    static string FormatPurpose(SceneNarratorGuidancePurpose purpose) => purpose switch
    {
        SceneNarratorGuidancePurpose.OpeningScene => "opening scene",
        SceneNarratorGuidancePurpose.LocationTransition => "location transition",
        SceneNarratorGuidancePurpose.TimeSkip => "time skip",
        SceneNarratorGuidancePurpose.SceneReset => "scene reset",
        _ => "scene setup"
    };

    static void AppendList(StringBuilder builder, string label, IReadOnlyList<SceneTransitionEntity> entities)
    {
        var text = entities.Count == 0
            ? "None"
            : string.Join(", ", entities.Select(entity => entity.Name));
        builder.AppendLine($"{label}: {text}.");
    }
}

public sealed class SceneTransitionValidationException(string message) : InvalidOperationException(message);
