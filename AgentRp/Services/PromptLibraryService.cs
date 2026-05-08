using System.Text;
using System.Text.RegularExpressions;
using AgentRp.Session;

namespace AgentRp.Services;

public static class PromptLibraryStageIds
{
    public const string Snapshot = "snapshot";
    public const string Appearance = "appearance";
    public const string Selection = "selection";
    public const string Planning = "planning";
    public const string Prose = "prose";
    public const string StoryAssistantBase = "storyAssistantBase";
    public const string StoryAssistantPrepareStory = "storyAssistantPrepareStory";
    public const string StoryAssistantIntroduceCharacters = "storyAssistantIntroduceCharacters";
    public const string StoryAssistantIntroduceLocation = "storyAssistantIntroduceLocation";
    public const string StoryAssistantChangeScene = "storyAssistantChangeScene";
}

public static class PromptLibraryStageGroups
{
    public const string Generation = "Generation";
    public const string StoryAssistant = "Story Assistant";
}

public sealed record PromptLibraryStageDefinition(
    string Id,
    string Label,
    bool HasTurnShapes,
    string Group);

public sealed record PromptLibraryPlaceholderDefinition(
    string Token,
    string Description,
    IReadOnlyList<string> StageIds);

public sealed record PromptRenderResult(
    string SystemPrompt,
    string UserPrompt);

public sealed partial class PromptLibraryService
{
    public const string ProseFormatReminder =
        "Format reminder: Always wrap actions in *asterisks* and speech in \"quotes\". "
        + "Every action must include an explicit subject pronoun or character name when potentially ambiguous, such as *She trembles* or *Bella looks away*. "
        + "Do not write bare action fragments like *trembles* or *eyes pleading*. Never output unwrapped output.";

    const string LegacyProseFormatRule = "Format: Always wrap actions in *asterisks* and speech in \"quotes\". Never output unwrapped output.";
    const string LegacyProseFormatReminder = "Format reminder: Always wrap actions in *asterisks* and speech in \"quotes\". Never output unwrapped output.";

    static readonly IReadOnlyList<PromptLibraryStageDefinition> StageDefinitions =
    [
        new(PromptLibraryStageIds.Snapshot, "Snapshot", false, PromptLibraryStageGroups.Generation),
        new(PromptLibraryStageIds.Appearance, "Appearance", false, PromptLibraryStageGroups.Generation),
        new(PromptLibraryStageIds.Selection, "Selection", false, PromptLibraryStageGroups.Generation),
        new(PromptLibraryStageIds.Planning, "Planning", true, PromptLibraryStageGroups.Generation),
        new(PromptLibraryStageIds.Prose, "Prose", true, PromptLibraryStageGroups.Generation),
        new(PromptLibraryStageIds.StoryAssistantBase, "Assistant Base", false, PromptLibraryStageGroups.StoryAssistant),
        new(PromptLibraryStageIds.StoryAssistantPrepareStory, "Prepare Story", false, PromptLibraryStageGroups.StoryAssistant),
        new(PromptLibraryStageIds.StoryAssistantIntroduceCharacters, "Introduce Characters", false, PromptLibraryStageGroups.StoryAssistant),
        new(PromptLibraryStageIds.StoryAssistantIntroduceLocation, "Introduce Location", false, PromptLibraryStageGroups.StoryAssistant),
        new(PromptLibraryStageIds.StoryAssistantChangeScene, "Change Scene", false, PromptLibraryStageGroups.StoryAssistant)
    ];

    static readonly IReadOnlyList<PromptLibraryPlaceholderDefinition> PlaceholderDefinitions =
    [
        Placeholder("{context}", "Full scene context in the default section order.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.actor}", "Actor section from the scene context.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.location}", "Location section from the scene context.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.charactersInScene}", "Characters currently in the scene.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.otherKnownCharacters}", "Known characters not currently present.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.objectsInScene}", "Objects currently in the scene.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.storyContext}", "Genre, setting, tone, and story direction.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.contentGuidance}", "Explicit and violent content guidance.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.historySummary}", "Timeline and prior history summary.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.snapshot}", "Latest snapshot summary.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.transcript}", "Transcript since the latest snapshot.", PromptLibraryStageIds.Snapshot, PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.earlierPrivateIntentContinuity}", "Older private intent continuity not already in transcript.", PromptLibraryStageIds.Snapshot, PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{context.characterAppearances}", "Current appearance state for present characters.", PromptLibraryStageIds.Snapshot, PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{actor.name}", "Current actor name.", PromptLibraryStageIds.Planning),
        Placeholder("{speaker.name}", "Current speaker name for prose.", PromptLibraryStageIds.Prose),
        Placeholder("{guidance}", "Guidance text, when supplied.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{guidanceSection}", "Guidance section with the default heading and spacing.", PromptLibraryStageIds.Planning, PromptLibraryStageIds.Prose),
        Placeholder("{requestedTurnShape}", "Requested turn shape label.", PromptLibraryStageIds.Planning),
        Placeholder("{requestedTurnShapeSection}", "Required turn-shape instructions, when supplied.", PromptLibraryStageIds.Planning),
        Placeholder("{turnScopeRules}", "Default planning turn scope rules.", PromptLibraryStageIds.Planning),
        Placeholder("{planning.turnShapeDefinitions}", "Editable planning turn-shape definitions.", PromptLibraryStageIds.Planning),
        Placeholder("{prose.turnShapeSystem}", "Editable prose system instructions for the selected turn shape.", PromptLibraryStageIds.Prose),
        Placeholder("{prose.turnShapeUser}", "Editable prose user reminder for the selected turn shape.", PromptLibraryStageIds.Prose),
        Placeholder("{prose.inSceneNames}", "Other in-scene names included in the prose system prompt.", PromptLibraryStageIds.Prose),
        Placeholder("{prose.narratorSystem}", "Narrator-specific system instruction.", PromptLibraryStageIds.Prose),
        Placeholder("{prose.characterOnlySystem}", "Character-only prose system instructions.", PromptLibraryStageIds.Prose),
        Placeholder("{planner.beat}", "Planner beat.", PromptLibraryStageIds.Prose),
        Placeholder("{planner.intent}", "Planner intent.", PromptLibraryStageIds.Prose),
        Placeholder("{planner.immediateGoal}", "Planner immediate goal.", PromptLibraryStageIds.Prose),
        Placeholder("{planner.changeIntroduced}", "Planner change introduced.", PromptLibraryStageIds.Prose),
        Placeholder("{planner.whyNow}", "Planner why-now rationale.", PromptLibraryStageIds.Prose),
        Placeholder("{planner.privateIntent}", "Planner private intent.", PromptLibraryStageIds.Prose),
        Placeholder("{planner.narrativeGuardrails}", "Planner narrative guardrails.", PromptLibraryStageIds.Prose),
        Placeholder("{content.explicitLabel}", "Explicit content label.", PromptLibraryStageIds.Appearance),
        Placeholder("{content.violentLabel}", "Violent content label.", PromptLibraryStageIds.Appearance),
        Placeholder("{appearance.characters}", "Appearance-stage character list.", PromptLibraryStageIds.Appearance),
        Placeholder("{appearance.transcript}", "Appearance-stage transcript.", PromptLibraryStageIds.Appearance),
        Placeholder("{selection.activeSpeakerName}", "Responder selection active speaker.", PromptLibraryStageIds.Selection),
        Placeholder("{selection.guidanceSection}", "Responder selection guidance block.", PromptLibraryStageIds.Selection),
        Placeholder("{selection.eligibleResponders}", "Responder selection candidate list.", PromptLibraryStageIds.Selection),
        Placeholder("{selection.locationSection}", "Responder selection location block.", PromptLibraryStageIds.Selection),
        Placeholder("{selection.storyContext}", "Responder selection story context block.", PromptLibraryStageIds.Selection),
        Placeholder("{selection.contentGuidance}", "Responder selection content guidance block.", PromptLibraryStageIds.Selection),
        Placeholder("{selection.recentTranscript}", "Responder selection recent transcript block.", PromptLibraryStageIds.Selection),
        Placeholder("{selection.currentAppearance}", "Responder selection appearance detail.", PromptLibraryStageIds.Selection),
        Placeholder("{snapshot.threadTitle}", "Snapshot thread title.", PromptLibraryStageIds.Snapshot),
        Placeholder("{snapshot.currentLocation}", "Snapshot current location.", PromptLibraryStageIds.Snapshot),
        Placeholder("{snapshot.characters}", "Snapshot character catalog.", PromptLibraryStageIds.Snapshot),
        Placeholder("{snapshot.locations}", "Snapshot location catalog.", PromptLibraryStageIds.Snapshot),
        Placeholder("{snapshot.items}", "Snapshot item catalog.", PromptLibraryStageIds.Snapshot),
        Placeholder("{snapshot.history}", "Snapshot canonical history summary.", PromptLibraryStageIds.Snapshot),
        Placeholder("{snapshot.messages}", "Snapshot included branch messages.", PromptLibraryStageIds.Snapshot)
    ];

    static readonly IReadOnlyDictionary<string, PromptLibraryPlaceholderDefinition> KnownPlaceholders =
        PlaceholderDefinitions.ToDictionary(x => x.Token[1..^1], StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> PlaceholdersByStage =
        StageDefinitions.ToDictionary(
            stage => stage.Id,
            stage => (IReadOnlySet<string>)PlaceholderDefinitions
                .Where(x => x.StageIds.Contains(stage.Id, StringComparer.Ordinal))
                .Select(x => x.Token[1..^1])
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

    public IReadOnlyList<PromptLibraryStageDefinition> Stages => StageDefinitions;

    public IReadOnlyList<PromptLibraryPlaceholderDefinition> Placeholders => PlaceholderDefinitions;

    public PromptLibraryState GetDefaultLibrary() => CreateDefaultState();

    public PromptLibraryState Normalize(PromptLibraryState? state) => NormalizeState(state);

    public void Validate(PromptLibraryState state) => ValidateState(state);

    public PromptRenderResult Render(PromptLibraryState state, string stageId, IReadOnlyDictionary<string, string> values) =>
        RenderStage(state, stageId, values);

    public static IReadOnlyList<PromptLibraryStageDefinition> GetStageDefinitions() => StageDefinitions;

    public static IReadOnlyList<PromptLibraryPlaceholderDefinition> GetPlaceholders(string stageId) =>
        PlaceholderDefinitions
            .Where(x => x.StageIds.Contains(stageId, StringComparer.Ordinal))
            .ToList();

    public static PromptLibraryState CreateDefaultState() => new()
    {
        Prompts = new(StringComparer.Ordinal)
        {
            [PromptLibraryStageIds.Snapshot] = new()
            {
                System = DefaultSnapshotSystemPrompt,
                User = DefaultSnapshotUserPromptTemplate
            },
            [PromptLibraryStageIds.Appearance] = new()
            {
                System = DefaultAppearanceSystemPrompt,
                User = DefaultAppearanceUserPromptTemplate
            },
            [PromptLibraryStageIds.Selection] = new()
            {
                System = DefaultSelectionSystemPrompt,
                User = DefaultSelectionUserPromptTemplate
            },
            [PromptLibraryStageIds.Planning] = new()
            {
                System = DefaultPlanningSystemPromptTemplate,
                User = DefaultPlanningUserPromptTemplate
            },
            [PromptLibraryStageIds.Prose] = new()
            {
                System = DefaultProseSystemPromptTemplate,
                User = DefaultProseUserPromptTemplate
            },
            [PromptLibraryStageIds.StoryAssistantBase] = new()
            {
                System = DefaultStoryAssistantBaseSystemPrompt,
                User = ""
            },
            [PromptLibraryStageIds.StoryAssistantPrepareStory] = new()
            {
                System = "",
                User = DefaultStoryAssistantPrepareStoryPrompt
            },
            [PromptLibraryStageIds.StoryAssistantIntroduceCharacters] = new()
            {
                System = "",
                User = DefaultStoryAssistantIntroduceCharactersPrompt
            },
            [PromptLibraryStageIds.StoryAssistantIntroduceLocation] = new()
            {
                System = "",
                User = DefaultStoryAssistantIntroduceLocationPrompt
            },
            [PromptLibraryStageIds.StoryAssistantChangeScene] = new()
            {
                System = "",
                User = DefaultStoryAssistantChangeScenePrompt
            }
        },
        TurnShapes = new(StringComparer.Ordinal)
        {
            [PromptLibraryStageIds.Planning] =
            [
                Shape("compact", "Compact", "one action beat, one or two phrases, optional short tag (always preferred)"),
                Shape("brief", "Brief", "one action beat, one to two short lines with a tag in between (rare)"),
                Shape("extended", "Extended", "elaborate the beat into three focused paragraphs with well choreography interactions (only when asked)"),
                Shape("monologue", "Monologue", "short monologue allowed (only when asked)"),
                Shape("silent", "Silent", "quick action/subtext only, no spoken lines (common)"),
                Shape("silent-monologue", "Silent Monologue", "extended action/subtext only, no spoken lines; detailed movement, touch, posture, expression, atmosphere, or implication across one playable move (common in intimate, physical, or subtext-heavy moments)")
            ],
            [PromptLibraryStageIds.Prose] =
            [
                Shape("compact", "Compact",
                    """
                    Write only a very short compact turn on the same line with:
                    - One brief visible *action*.
                    - One or two short "spoken phrases".
                    - One very short trailing *action* if needed.
                    """),
                Shape("brief", "Brief",
                    """
                    Write only a very brief turn on the same line with:
                    - One brief *action*.
                    - One or two short "spoken lines" separated by simple *action*.
                    """),
                Shape("extended", "Extended",
                    """
                    Write an extended turn with:
                    - One to three paragraphs.
                    - "Dialogue", *action*, and narration that all serve the same planned beat.
                    - A clear landing point before the turn becomes a second move.
                    """),
                Shape("monologue", "Monologue",
                    """
                    Write a brief monologue turn with:
                    - Sentences of "spoken word"s with simple *action* in between.
                    - Flow this into a connected move with a clear landing point.
                    - Stop before repeating.
                    """),
                Shape("silent", "Silent",
                    """
                    Write only a quick silent turn on the same line with:
                    - Nonverbal move or subtext *action* and no verbal component.
                    - Prefer action, expression, posture, or a small physical response.
                    - Do not use "dialogue" unless a word or two is necessary to land the beat.
                    - Keep it restrained and readable.
                    """),
                Shape("silent-monologue", "Silent Monologue",
                    """
                    Write only a silent monologue turn with:
                    - Detailed nonverbal *action* and subtext only.
                    - Use touch, movement, posture, expression, distance, hesitation, or atmosphere.
                    - Build one connected physical move with a clear landing point.
                    - Do not use "dialogue" or explain the subtext directly.
                    - Stop before it becomes a sequence or exposition.
                    """)
            ]
        }
    };

    public static PromptLibraryState NormalizeState(PromptLibraryState? state)
    {
        var defaults = CreateDefaultState();
        if (state is null)
            return defaults;

        var normalized = CreateDefaultState();
        foreach (var pair in defaults.Prompts)
        {
            if (!state.Prompts.TryGetValue(pair.Key, out var prompt))
                continue;

            normalized.Prompts[pair.Key] = new()
            {
                System = prompt.System,
                User = prompt.User
            };
        }

        foreach (var pair in defaults.TurnShapes)
        {
            var existing = state.TurnShapes.TryGetValue(pair.Key, out var shapes)
                ? shapes.ToDictionary(x => x.Id, StringComparer.Ordinal)
                : [];
            normalized.TurnShapes[pair.Key] = pair.Value.Select(shape =>
            {
                var value = existing.TryGetValue(shape.Id, out var configured)
                    ? configured.Value
                    : shape.Value;
                return new ShapePromptState
                {
                    Id = shape.Id,
                    Label = shape.Label,
                    Value = value
                };
            }).ToList();
        }

        return normalized;
    }

    public static PromptRenderResult RenderStage(PromptLibraryState state, string stageId, IReadOnlyDictionary<string, string> values)
    {
        var normalized = NormalizeState(state);
        var prompt = normalized.Prompts.TryGetValue(stageId, out var configured)
            ? configured
            : CreateDefaultState().Prompts[stageId];
        return new(
            RenderTemplate(prompt.System, values),
            RenderTemplate(prompt.User, values));
    }

    public static string WithProseFormatReminder(string userPrompt)
    {
        var body = userPrompt.Replace("\r\n", "\n", StringComparison.Ordinal);
        body = RemoveProseFormatReminderLine(body, LegacyProseFormatRule);
        body = RemoveProseFormatReminderLine(body, LegacyProseFormatReminder);
        body = RemoveProseFormatReminderLine(body, ProseFormatReminder).TrimEnd();

        return string.IsNullOrWhiteSpace(body)
            ? ProseFormatReminder
            : $"{body}\n\n{ProseFormatReminder}";
    }

    public static string RenderPrompt(PromptLibraryState state, string stageId, string field, IReadOnlyDictionary<string, string> values)
    {
        var prompt = RenderStage(state, stageId, values);
        return string.Equals(field, "system", StringComparison.Ordinal)
            ? prompt.SystemPrompt
            : prompt.UserPrompt;
    }

    public static void ValidateState(PromptLibraryState state)
    {
        var normalized = NormalizeState(state);
        foreach (var pair in normalized.Prompts)
        {
            ValidateTemplate(pair.Value.System, pair.Key);
            ValidateTemplate(pair.Value.User, pair.Key);
        }

        foreach (var pair in normalized.TurnShapes)
        {
            foreach (var shape in pair.Value)
                ValidateTemplate(shape.Value, pair.Key);
        }
    }

    public static string StripComments(string template)
    {
        var builder = new StringBuilder(template.Length);
        for (var i = 0; i < template.Length; i++)
        {
            if (i + 1 < template.Length && template[i] == '/' && template[i + 1] == '*')
            {
                i += 2;
                var foundEnd = false;
                while (i + 1 < template.Length)
                {
                    if (template[i] == '/' && template[i + 1] == '*')
                        throw new InvalidOperationException("Saving the prompt library failed because template comments cannot be nested.");

                    if (template[i] == '*' && template[i + 1] == '/')
                    {
                        i++;
                        foundEnd = true;
                        break;
                    }

                    i++;
                }

                if (!foundEnd)
                    throw new InvalidOperationException("Saving the prompt library failed because a template comment was not closed.");

                continue;
            }

            builder.Append(template[i]);
        }

        return builder.ToString();
    }

    public static string FormatTurnShapeDefinitions(IReadOnlyList<ShapePromptState> shapes)
    {
        var ordered = OrderTurnShapes(shapes);
        return string.Join(Environment.NewLine, ordered.Select(shape => $"- {FormatTurnShapeLabel(shape.Label)} = {shape.Value}"));
    }

    public static string GetTurnShapeTemplate(PromptLibraryState state, string stageId, string requestedTurnShape)
    {
        var normalized = NormalizeState(state);
        var shapes = normalized.TurnShapes.TryGetValue(stageId, out var stageShapes)
            ? stageShapes
            : [];
        return shapes.FirstOrDefault(shape =>
                string.Equals(shape.Id, ShapeId(requestedTurnShape), StringComparison.Ordinal)
                || string.Equals(shape.Label, requestedTurnShape, StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?? shapes.FirstOrDefault()?.Value
            ?? string.Empty;
    }

    public static string BuildDefaultProseSystemTurnShape(string requestedTurnShape) => ShapeId(requestedTurnShape) switch
    {
        "compact" =>
            """
            This turn has a compact shape, fulfill the beat with one sharp move.
            - Keep this very short.
            - Start with one brief visible action or reaction.
            - Follow with one or two short spoken phrases.
            - Optionally add one very short trailing tag if needed.
            - Stop immediately.
            - Do not add a second move.
            """,
        "brief" =>
            """
            This turn has a brief shape, fulfill the beat with a quick move that may need a little setup or follow-through.
            - Keep this short.
            - Start with one brief action or reaction.
            - Follow with one or two short spoken lines separated by simple action.
            - Stop immediately.
            - Do not add a new topic or second emotional turn.
            """,
        "extended" =>
            """
            This turn has an extended shape, fulfill the beat and expand on it.
            - Expand the beat into three paragraphs with detailed choreography and vivid descriptions.
            - Use each paragraph well to create meaningful visuals.
            - Dialogue, action, and narration are allowed when they serve the immediate goal.
            - Provide a clear landing point.
            - Do not ramble, recap, or drift into a second move.
            """,
        "monologue" =>
            """
            This turn has a monologue shape, fulfill the beat with a longer move.
            - A longer reply is allowed here. You can make up to three connected beats in a row.
            - Up to five sentenses maximum of spoken words with simple actions in between.
            - Still focus on one beat but expand it into three parts.
            - Provide a clear landing point.
            - Do not ramble, recap, or drift into a second move.
            """,
        "silent" =>
            """
            This turn has a silent shape, fulfill the beat with a nonverbal move or subtext and no verbal component.
            - Prefer action, expression, posture, or a small physical response.
            - Do not use dialogue unless a word or two is necessary to land the beat.
            - Keep it restrained and readable.
            - Stop early once action is clear.
            """,
        "silent-monologue" =>
            """
            This turn has a silent monologue shape, fulfill the beat with a longer nonverbal move and no dialogue.
            - Use connected physical detail: touch, movement, posture, expression, distance, atmosphere, or subtext.
            - Let the action imply the emotional or tactical shift without explaining it.
            - Keep this to one playable move, not a full scene sequence.
            - Provide a clear landing point.
            - Do not use spoken words.
            - Do not ramble, recap, or drift into exposition.
            """,
        _ => string.Empty
    };

    static string RenderTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        var normalizedValues = values.ToDictionary(
            pair => NormalizePlaceholderKey(pair.Key),
            pair => pair.Value,
            StringComparer.Ordinal);
        var uncommented = StripComments(template);
        var rendered = PlaceholderRegex().Replace(uncommented, match =>
        {
            var key = match.Groups["key"].Value;
            return normalizedValues.TryGetValue(key, out var value) ? value : match.Value;
        });
        return rendered.TrimEnd();
    }

    static void ValidateTemplate(string template, string stageId)
    {
        var uncommented = StripComments(template);
        var allowed = PlaceholdersByStage.TryGetValue(stageId, out var stagePlaceholders)
            ? stagePlaceholders
            : throw new InvalidOperationException($"Saving the prompt library failed because '{stageId}' is not a supported prompt stage.");

        foreach (Match match in PlaceholderRegex().Matches(uncommented))
        {
            var key = match.Groups["key"].Value;
            if (!KnownPlaceholders.ContainsKey(key))
                throw new InvalidOperationException($"Saving the prompt library failed because '{{{key}}}' is not a supported placeholder.");

            if (!allowed.Contains(key))
                throw new InvalidOperationException($"Saving the prompt library failed because '{{{key}}}' is not available for the selected prompt stage.");
        }
    }

    static PromptLibraryPlaceholderDefinition Placeholder(string token, string description, params string[] stageIds) =>
        new(token, description, stageIds);

    static ShapePromptState Shape(string id, string label, string value) => new()
    {
        Id = id,
        Label = label,
        Value = value
    };

    static IEnumerable<ShapePromptState> OrderTurnShapes(IEnumerable<ShapePromptState> shapes)
    {
        var order = new[] { "compact", "silent", "silent-monologue", "brief", "extended", "monologue" };
        var shapeList = shapes.ToList();
        foreach (var id in order)
        {
            var match = shapeList.FirstOrDefault(shape => shape.Id == id);
            if (match is not null)
                yield return match;
        }

        foreach (var shape in shapeList.Where(shape => !order.Contains(shape.Id, StringComparer.Ordinal)))
            yield return shape;
    }

    static string NormalizePlaceholderKey(string key) =>
        key.StartsWith('{') && key.EndsWith('}') ? key[1..^1] : key;

    static string ShapeId(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "-");

    static string FormatTurnShapeLabel(string value) =>
        value.Trim().ToLowerInvariant();

    const string DefaultSnapshotSystemPrompt =
        """
        You create structured story snapshots from a selected branch transcript.
        Summarize only what is supported by the included messages and supplied story state.
        Return a concise narrative summary, then propose timeline entries that should be saved.
        Prefer durable developments over throwaway phrasing.
        Do not invent names, references, or events that are not grounded in the provided material.
        """;

    const string DefaultSnapshotUserPromptTemplate =
        """
        Thread title: {snapshot.threadTitle}
        Current location: {snapshot.currentLocation}
        Characters in story: {snapshot.characters}
        Locations in story: {snapshot.locations}
        Items in story: {snapshot.items}
        Existing canonical history: {snapshot.history}

        Included branch messages:
        {snapshot.messages}

        Return:
        1. A narrative summary of what has happened so far in this included range.
        2. Proposed timeline entries that should be added.
        For characterNames, locationNames, and itemNames, only use names from the provided catalogs.
        """;

    const string DefaultAppearanceSystemPrompt =
        """
        You update character scene state.

        Return structured output only.

        Scene state is what is visibly true about each character right now:
        clothing, carried items, body position, location, posture, visible condition, and current physical contact with people or objects.

        Use the prior scene state as the starting point.
        Use the latest transcript to update it.

        Keep stable details from the prior state unless the transcript changes or contradicts them.
        Stable details include clothing, carried items, injuries, location, posture, and physical contact.

        Do not drop outfits or carried items just because they were not mentioned again.

        Temporary details fade unless the latest transcript still supports them.
        Temporary details include facial expressions, brief gestures, glances, momentary touches, and passing reactions.

        For each character:
        - keep unchanged stable details regardless of percieved importance
        - keep details about what is exposed or not being worn
        - add new visible details
        - replace changed details
        - remove contradicted details
        - account for arms and legs, make sure only two of each are visible and they are in plausible positions
        - prevent lingering hand/arm positions that are not plausible to still be current

        Write only the current snapshot.
        Do not recap actions.
        Do not explain changes.
        Do not include thoughts, motives, memories, or personality.

        Return one result for every character currently in the scene.
        If a character has no supported current scene state, set hasCurrentSceneState to false and currentSceneState to "".

        The summary must mention only characters with hasCurrentSceneState true.
        """;

    const string DefaultAppearanceUserPromptTemplate =
        """
        Content guidance:
        - Explicit content: {content.explicitLabel}
        - Violent content: {content.violentLabel}

        Characters in the scene with initial appearance:
        {appearance.characters}

        **Transcript:**
        {appearance.transcript}

        Return one decision for every character currently in the scene.

        For each character, resolve the best supported current scene state from the transcript plus prior current scene state.

        Important:
        - Eagerly replace outdated prior details with newer supported details
        - Do not update by appending history
        - Describe only what is true now
        - Include where the character is relative to other characters, furniture, and objects when supported
        - Include current interaction with sheets, bed, doorway, wall, chair, or other visible scene elements when supported
        - If a prior detail is not reaffirmed and may no longer be true, leave it out
        - Forbidden means do not include that kind of detail
        - Allowed means include it only when naturally supported
        - Encouraged means prefer supported detail over softening it, but never invent it
        """;

    const string DefaultSelectionSystemPrompt =
        """
        You choose who in the current scene should respond next in an automatic story chat.
        Return only structured data.

        Choose exactly one responder from the provided candidate list.
        Never choose the active speaker.
        Never choose the narrator.
        Never choose someone who is not currently present in the scene.

        Pick the responder who creates the most interesting immediate next turn for this exact moment.
        Favor the character with the strongest local reason, pressure, opportunity, or emotional stake to answer now.
        """;

    const string DefaultSelectionUserPromptTemplate =
        """
        **Active speaker:** {selection.activeSpeakerName}
        - This speaker must not be selected as the responder.

        {selection.guidanceSection}
        **Eligible responders:**
        {selection.eligibleResponders}

        {selection.locationSection}
        {selection.storyContext}
        {selection.contentGuidance}
        **Recent transcript:**
        {selection.recentTranscript}

        **Current appearance state:**
        {selection.currentAppearance}

        Choose one name from the eligible responders list and explain briefly why they should answer next right now.
        """;

    const string DefaultPlanningSystemPromptTemplate =
        """
        You are the planning stage for a story scene message generator.
        Decide the next turn before any prose is written.
        Return only a concise structured plan.

        Stay grounded in the provided story context, scene state, character facts, and transcript.
        Plan one turn only.
        Choose one immediate beat, not a sequence.

        Build the plan using these fields:
        - Turn shape: choose exactly one of compact, brief, extended, monologue, silent, or silent monologue.
        - Beat: the kind of move being made in this turn.
        - Intent: the actor's immediate intention.
        - Immediate goal: what this turn tries to achieve right now.
        - Why now: why this beat fits this exact moment in the transcript.
        - Change introduced: what becomes different after this turn.
        - Private Intent: the actor's private continuity note for the hidden reason, feeling, agenda, fear, memory, sensation, concealed object, concealed action, or unspoken detail behind this turn.
        - Narrative Guardrails: avoid making the beat less effective or interesting
        - Content Guardrails: avoid introducing any sexual or violent content here

        Turn shape definitions:
        {planning.turnShapeDefinitions}

        Prioritize compact, silent, and silent monologue almost always.
        - Favor silent turns for quick intimate moments.
        - Favor silent monologue when an intimate, physical, or subtext-heavy moment needs a longer nonverbal beat instead of speech.
        - Don't eagerly follow the narrative if it is counter to character goals or private intent.
        - Pick the most valuable next beat to move the story forward, not the safest or most literal reply.
        - Identify when the current thread has run it's course and move on.
        - If a direct reaction is needed, react.
        - If no direct reaction is needed, introduce a small new beat that moves the scene.
        - Never end an exchange.
        - Never end a conversation.

        **strong beat:** changes something, shifts pressure, tests a boundary, redirects attention, creates a question, adds discomfort, adds intimacy, or forces a reply.

        Avoid empty beats.
        Do not only restate rules, confirm the current situation, paraphrase the last line, or preserve the same tension without adding value.
        Do not repeat parts of the story, always keep it moving.

        Be sure to account for surprising events where the character may be in shock or confused for a turn or two, while recovering or processing what just happened.

        Private Intent usage:
        - Use prior Private Intent entries as continuity, not commands.
        - Let them bias the actor's next beat when they still fit the visible scene.
        - Preserve or develop the actor's hidden motive when it remains relevant.
        - Return to older private intents when the visible scene creates a natural opening.
        - Update Private Intent each turn with the current hidden reason behind the planned beat.
        - Include necessary context for continuity.
        - Include only the actor's private feelings, wants, fears, suspicions, avoidance, hidden agenda, private physical sensations, concealed objects, concealed gestures, or things they are trying to make happen.
        - Invisible details are allowed if they belong to the actor and may affect future behavior.
        - Other characters do not know this Private Intent unless it becomes visible in the transcript.
        - Do not include other characters' private thoughts.
        - If the actor infers another character's motive, phrase it as the actor's suspicion, not fact.
        - Do not use Private Intent to duplicate the visible message. Visible actions belong in the transcript. Private Intent may include hidden/internal/concealed actions or sensations that are not visible to others.

        For intimate interactions, prioritize a silent monologue to detail the physical and emotional sensations in descriptive graphic explicit detail.
        For complex explainations of feelings, intentions, or situations, a monologue turn is appropriate.

        Do not write the final message text.
        """;

    const string DefaultPlanningUserPromptTemplate =
        """
        {context}

        {guidanceSection}
        {requestedTurnShapeSection}

        {turnScopeRules}
        """;

    const string DefaultProseSystemPromptTemplate =
        """
        You are {speaker.name} in a fictional chat between {prose.inSceneNames} and yourself.

        Write {speaker.name}'s next message only.

        Follow the planner's beat.
        Make one short playable move, then stop.

        Priority order:
        1. Fulfill the beat
        2. Stay true to {speaker.name}, the current scene, and recent transcript
        3. Use as few words as possible
        4. Stop at the first natural pause

        Respect the supplied story context and content guidance.

        {prose.narratorSystem}{prose.characterOnlySystem}
        """;

    const string DefaultProseUserPromptTemplate =
        """
        {context}

        {guidanceSection}

        Write the turn by fulfilling only:
        1. **the beat:** {planner.beat}
        2. **the intent:** {planner.intent}
        3. **the immediate goal:** {planner.immediateGoal}
        4. **the change introduced:** {planner.changeIntroduced}
        5. **why now:** {planner.whyNow}
        6. **private intent:** {planner.privateIntent}
        7. **narrative guardrails:** {planner.narrativeGuardrails}
        - Honor why now and the guardrails.
        - Let private intent influence the actor's subtext and choices, but do not reveal it directly unless the planned beat naturally makes some part visible.
        - Do not expand beyond them.
        - Stop early to prevent ramble, recap, or repeating yourself.

        CRITICAL STEPS: {prose.turnShapeUser}
        - Stop

        Format reminder: Always wrap actions in *asterisks* and speech in "quotes". Every action must include an explicit subject pronoun or character name when potentially ambiguous, such as *She trembles* or *Bella looks away*. Do not write bare action fragments like *trembles* or *eyes pleading*. Never output unwrapped output.
        """;

    static string RemoveProseFormatReminderLine(string prompt, string line)
    {
        var updated = prompt
            .Replace($"\n\n{line}\n\n", "\n\n", StringComparison.Ordinal)
            .Replace($"\n{line}\n", "\n", StringComparison.Ordinal);

        if (updated.StartsWith($"{line}\n\n", StringComparison.Ordinal))
            updated = updated[(line.Length + 2)..];

        if (updated.StartsWith($"{line}\n", StringComparison.Ordinal))
            updated = updated[(line.Length + 1)..];

        if (updated.EndsWith($"\n\n{line}", StringComparison.Ordinal))
            updated = updated[..^(line.Length + 2)];

        if (updated.EndsWith($"\n{line}", StringComparison.Ordinal))
            updated = updated[..^(line.Length + 1)];

        return string.Equals(updated, line, StringComparison.Ordinal)
            ? ""
            : updated;
    }

    [GeneratedRegex(@"\{(?<key>[A-Za-z0-9_.]+)\}")]
    private static partial Regex PlaceholderRegex();
}
