using System.Text;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed class TranscriptPromptContextBuilder
{
    public TurnPromptContext BuildTurnContext(RpChatDocument document, string parentTurnId, string guidance, string requestedTurnShape, RpCharacter? requestedActor)
    {
        var activePath = TranscriptGraph.GetActivePath(document.Transcript);
        if (!string.IsNullOrWhiteSpace(parentTurnId))
        {
            var parentIndex = activePath.FindIndex(turn => turn.Id == parentTurnId);
            if (parentIndex >= 0)
                activePath = activePath.Take(parentIndex + 1).ToList();
        }

        var snapshot = activePath.Count == 0
            ? null
            : document.Transcript.Snapshots
                .Where(candidate => activePath.Any(turn => turn.Id == candidate.TurnId))
                .OrderBy(candidate => candidate.CreatedUtc)
                .LastOrDefault();
        var snapshotTurnIndex = snapshot is null
            ? -1
            : activePath.FindIndex(turn => turn.Id == snapshot.TurnId);
        var transcriptTurns = snapshotTurnIndex >= 0
            ? activePath.Skip(snapshotTurnIndex + 1).ToList()
            : activePath;

        var scene = activePath.LastOrDefault()?.Scene ?? document.Transcript.RootScene;
        var presentCharacters = document.Characters.Where(character => scene.InSceneCharacterIds.Contains(character.Id)).ToList();
        var characterAppearances = BuildAppearanceMap(snapshot, activePath, snapshotTurnIndex);
        var actor = requestedActor ?? presentCharacters.FirstOrDefault() ?? document.Characters.FirstOrDefault();
        var requestedShape = string.IsNullOrWhiteSpace(requestedTurnShape) ? "Brief" : requestedTurnShape;
        var planningDefinitions = FormatTurnShapeDefinitions(document, "planning");

        return new(
            Actor: actor,
            SnapshotText: snapshot is null ? "No pinned snapshot yet." : snapshot.Summary,
            TranscriptText: FormatTranscript(transcriptTurns),
            CharactersText: FormatCharacters(presentCharacters),
            CharacterAppearancesText: FormatCharacterAppearances(presentCharacters, characterAppearances),
            AppearanceCharactersText: FormatAppearanceCharacters(presentCharacters, characterAppearances),
            AppearanceTranscriptText: FormatTranscript(transcriptTurns),
            EarlierPrivateIntentContinuity: BuildEarlierPrivateIntentContinuity(snapshot, activePath, snapshotTurnIndex),
            Guidance: guidance,
            GuidanceSection: string.IsNullOrWhiteSpace(guidance) ? "" : $"Guidance:\n{guidance.Trim()}",
            RequestedTurnShape: requestedShape,
            RequestedTurnShapeSection: BuildRequestedTurnShapeSection(document, requestedShape),
            PlanningTurnShapeDefinitions: planningDefinitions,
            TurnScopeRules: "Stay in one present-tense beat. Keep continuity exact. Do not skip ahead or summarize future action.");
    }

    public SnapshotPromptContext BuildSnapshotContext(RpChatDocument document, string turnId)
    {
        var activePath = TranscriptGraph.GetActivePath(document.Transcript);
        var turnIndex = activePath.FindIndex(turn => turn.Id == turnId);
        if (turnIndex >= 0)
            activePath = activePath.Take(turnIndex + 1).ToList();

        var currentTurn = activePath.LastOrDefault();
        var scene = currentTurn?.Scene ?? document.Transcript.RootScene;
        var presentCharacters = document.Characters.Where(character => scene.InSceneCharacterIds.Contains(character.Id)).ToList();
        var characterAppearances = BuildAppearanceMap(null, activePath, -1);

        return new(
            TranscriptText: FormatTranscript(activePath),
            CharacterAppearancesText: FormatCharacterAppearances(presentCharacters, characterAppearances),
            EarlierPrivateIntentContinuity: BuildEarlierPrivateIntentContinuity(null, activePath, -1));
    }

    public Dictionary<string, string> BuildTokens(TurnPromptContext context, string planningOutput)
    {
        var actorName = context.Actor?.Name ?? "Narrator";
        return new(StringComparer.Ordinal)
        {
            ["{appearance.characters}"] = context.AppearanceCharactersText,
            ["{appearance.transcript}"] = context.AppearanceTranscriptText,
            ["{context.characters}"] = context.CharactersText,
            ["{context.snapshot}"] = context.SnapshotText,
            ["{context.transcript}"] = context.TranscriptText,
            ["{context.characterAppearances}"] = context.CharacterAppearancesText,
            ["{context.earlierPrivateIntentContinuity}"] = context.EarlierPrivateIntentContinuity,
            ["{actor.name}"] = actorName,
            ["{guidance}"] = context.Guidance,
            ["{guidanceSection}"] = context.GuidanceSection,
            ["{requestedTurnShape}"] = context.RequestedTurnShape,
            ["{requestedTurnShapeSection}"] = context.RequestedTurnShapeSection,
            ["{planning.turnShapeDefinitions}"] = context.PlanningTurnShapeDefinitions,
            ["{turnScopeRules}"] = context.TurnScopeRules,
            ["{planning.output}"] = planningOutput
        };
    }

    public Dictionary<string, string> BuildTokens(SnapshotPromptContext context) => new(StringComparer.Ordinal)
    {
        ["{context.transcript}"] = context.TranscriptText,
        ["{context.characterAppearances}"] = context.CharacterAppearancesText,
        ["{context.earlierPrivateIntentContinuity}"] = context.EarlierPrivateIntentContinuity
    };

    static Dictionary<string, string> BuildAppearanceMap(RpTranscriptSnapshot? snapshot, IReadOnlyList<RpTranscriptTurn> path, int snapshotTurnIndex)
    {
        var appearances = snapshot?.CharacterAppearances.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var startIndex = snapshotTurnIndex >= 0 ? snapshotTurnIndex + 1 : 0;
        foreach (var turn in path.Skip(startIndex))
        {
            foreach (var pair in turn.AppearanceByCharacterId)
                appearances[pair.Key] = pair.Value;
        }

        return appearances;
    }

    static string BuildEarlierPrivateIntentContinuity(RpTranscriptSnapshot? snapshot, IReadOnlyList<RpTranscriptTurn> path, int snapshotTurnIndex)
    {
        var builder = new StringBuilder();
        if (snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.EarlierPrivateIntentContinuity))
            builder.AppendLine(snapshot.EarlierPrivateIntentContinuity.Trim());

        var endIndex = snapshotTurnIndex >= 0 ? snapshotTurnIndex : path.Count;
        foreach (var turn in path.Take(endIndex))
        {
            foreach (var pair in turn.PrivateIntentByCharacterId.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
                builder.AppendLine($"{turn.ActorName}: {pair.Value}");
        }

        return builder.ToString().Trim();
    }

    static string FormatTranscript(IEnumerable<RpTranscriptTurn> turns)
    {
        var lines = turns
            .Where(turn => !string.IsNullOrWhiteSpace(turn.Body))
            .Select(turn => $"{turn.AuthorName}: {turn.Body.Trim()}");
        var content = string.Join("\n\n", lines);
        return string.IsNullOrWhiteSpace(content) ? "(No transcript yet.)" : content;
    }

    static string FormatCharacters(IEnumerable<RpCharacter> characters)
    {
        var lines = characters.Select(character => $"{character.Name}: {character.Summary}".Trim());
        var content = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(content) ? "(No active characters.)" : content;
    }

    static string FormatAppearanceCharacters(IEnumerable<RpCharacter> characters, IReadOnlyDictionary<string, string> appearanceMap)
    {
        var lines = characters.Select(character =>
        {
            var appearance = appearanceMap.TryGetValue(character.Id, out var current) && !string.IsNullOrWhiteSpace(current)
                ? current
                : character.Appearance;
            return $"{character.Name}: {appearance}".Trim();
        });
        var content = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(content) ? "(No character appearance state.)" : content;
    }

    static string FormatCharacterAppearances(IEnumerable<RpCharacter> characters, IReadOnlyDictionary<string, string> appearanceMap)
    {
        var lines = characters
            .Select(character => appearanceMap.TryGetValue(character.Id, out var appearance) && !string.IsNullOrWhiteSpace(appearance)
                ? $"{character.Name}: {appearance}"
                : null)
            .Where(line => !string.IsNullOrWhiteSpace(line));
        var content = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(content) ? "(No current appearance state.)" : content;
    }

    static string BuildRequestedTurnShapeSection(RpChatDocument document, string requestedTurnShape)
    {
        var match = document.PromptLibrary.TurnShapes.TryGetValue("planning", out var shapes)
            ? shapes.FirstOrDefault(shape => string.Equals(shape.Label, requestedTurnShape, StringComparison.OrdinalIgnoreCase))
            : null;
        return match is null
            ? ""
            : $"Requested turn shape: {requestedTurnShape}\n{match.Value}";
    }

    static string FormatTurnShapeDefinitions(RpChatDocument document, string stepId)
    {
        if (!document.PromptLibrary.TurnShapes.TryGetValue(stepId, out var shapes))
            return "";

        var lines = shapes.Select(shape => $"{shape.Label}: {shape.Value}");
        return string.Join("\n", lines);
    }
}

public sealed record TurnPromptContext(
    RpCharacter? Actor,
    string SnapshotText,
    string TranscriptText,
    string CharactersText,
    string CharacterAppearancesText,
    string AppearanceCharactersText,
    string AppearanceTranscriptText,
    string EarlierPrivateIntentContinuity,
    string Guidance,
    string GuidanceSection,
    string RequestedTurnShape,
    string RequestedTurnShapeSection,
    string PlanningTurnShapeDefinitions,
    string TurnScopeRules);

public sealed record SnapshotPromptContext(
    string TranscriptText,
    string CharacterAppearancesText,
    string EarlierPrivateIntentContinuity);

public static class PromptTemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> tokens)
    {
        var rendered = template;
        foreach (var pair in tokens)
            rendered = rendered.Replace(pair.Key, pair.Value ?? "", StringComparison.Ordinal);

        return rendered.Trim();
    }
}
