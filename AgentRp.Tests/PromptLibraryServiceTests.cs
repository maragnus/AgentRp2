using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class PromptLibraryServiceTests
{
    [Fact]
    public void DefaultsMatchAgentRp1PromptLibraryContentWithAgentRp2ProseFormatReminder()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        AssertPromptEqual(ExtractFirstRawStringAfter(AgentRp1Path("Services", "StoryScenePrompts", "StorySceneAppearancePromptBuilder.cs"), "BuildSystemPrompt"), defaults.Prompts[PromptLibraryStageIds.Appearance].System);
        AssertPromptEqual(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultAppearanceUserPromptTemplate"), defaults.Prompts[PromptLibraryStageIds.Appearance].User);
        AssertPromptEqual(ExtractFirstRawStringAfter(AgentRp1Path("Services", "StoryScenePrompts", "StorySceneResponderSelectionPromptBuilder.cs"), "BuildSystemPrompt"), defaults.Prompts[PromptLibraryStageIds.Selection].System);
        AssertPromptEqual(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultSelectionUserPromptTemplate"), defaults.Prompts[PromptLibraryStageIds.Selection].User);
        AssertPromptEqual(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultPlanningSystemPromptTemplate"), defaults.Prompts[PromptLibraryStageIds.Planning].System);
        AssertPromptEqual(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultPlanningUserPromptTemplate"), defaults.Prompts[PromptLibraryStageIds.Planning].User);
        AssertPromptEqual(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultProseSystemPromptTemplate"), defaults.Prompts[PromptLibraryStageIds.Prose].System);
        AssertPromptEqual(
            PromptLibraryService.WithProseFormatReminder(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultProseUserPromptTemplate")),
            defaults.Prompts[PromptLibraryStageIds.Prose].User);
        AssertPromptEqual(ExtractFirstRawStringAfter(AgentRp1Path("Services", "StoryChatSnapshotService.cs"), "BuildSnapshotSystemPrompt"), defaults.Prompts[PromptLibraryStageIds.Snapshot].System);
        AssertPromptEqual(ExpectedSnapshotUserPromptTemplate, defaults.Prompts[PromptLibraryStageIds.Snapshot].User);
    }

    [Fact]
    public void TurnShapeDefaultsMatchAgentRp1Exactly()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        Assert.Equal("one action beat, one or two phrases, optional short tag (always preferred)", Shape(defaults, PromptLibraryStageIds.Planning, "compact"));
        Assert.Equal("one action beat, one to two short lines with a tag in between (rare)", Shape(defaults, PromptLibraryStageIds.Planning, "brief"));
        Assert.Equal("elaborate the beat into three focused paragraphs with well choreography interactions (only when asked)", Shape(defaults, PromptLibraryStageIds.Planning, "extended"));
        Assert.Equal("short monologue allowed (only when asked)", Shape(defaults, PromptLibraryStageIds.Planning, "monologue"));
        Assert.Equal("quick action/subtext only, no spoken lines (common)", Shape(defaults, PromptLibraryStageIds.Planning, "silent"));
        Assert.Equal("extended action/subtext only, no spoken lines; detailed movement, touch, posture, expression, atmosphere, or implication across one playable move (common in intimate, physical, or subtext-heavy moments)", Shape(defaults, PromptLibraryStageIds.Planning, "silent-monologue"));
        AssertPromptEqual(ExpectedPlanningTurnShapeDefinitions, PromptLibraryService.FormatTurnShapeDefinitions(defaults.TurnShapes[PromptLibraryStageIds.Planning]));
        AssertPromptEqual(ExpectedProseBriefTurnShape, Shape(defaults, PromptLibraryStageIds.Prose, "brief"));
        AssertPromptEqual(ExpectedProseSilentMonologueTurnShape, Shape(defaults, PromptLibraryStageIds.Prose, "silent-monologue"));
    }

    [Fact]
    public void RenderReplacesSupportedPlaceholdersAndStripsComments()
    {
        var state = PromptLibraryService.CreateDefaultState();
        state.Prompts[PromptLibraryStageIds.Planning] = new()
        {
            System = "Hello {actor.name} /* hidden {actor.name} */",
            User = "{context.transcript}"
        };

        var rendered = PromptLibraryService.RenderStage(
            state,
            PromptLibraryStageIds.Planning,
            new Dictionary<string, string>
            {
                ["{actor.name}"] = "Bella",
                ["{context.transcript}"] = "Transcript text"
            });

        Assert.Equal("Hello Bella", rendered.SystemPrompt);
        Assert.Equal("Transcript text", rendered.UserPrompt);
    }

    [Fact]
    public void ValidateRejectsUnsupportedPlaceholder()
    {
        var state = PromptLibraryService.CreateDefaultState();
        state.Prompts[PromptLibraryStageIds.Planning].System = "{not.real}";

        var exception = Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(state));

        Assert.Contains("not.real", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsPlaceholderFromWrongStage()
    {
        var state = PromptLibraryService.CreateDefaultState();
        state.Prompts[PromptLibraryStageIds.Appearance].System = "{actor.name}";

        var exception = Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(state));

        Assert.Contains("not available", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsRemovedCompatibilityPlaceholders()
    {
        var contextCharacters = PromptLibraryService.CreateDefaultState();
        contextCharacters.Prompts[PromptLibraryStageIds.Planning].System = "{context.characters}";
        var contextException = Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(contextCharacters));
        Assert.Contains("not a supported", contextException.Message, StringComparison.Ordinal);

        var planningOutput = PromptLibraryService.CreateDefaultState();
        planningOutput.Prompts[PromptLibraryStageIds.Prose].User = "{planning.output}";
        var planningException = Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(planningOutput));
        Assert.Contains("not a supported", planningException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateUsesAgentRp1StageAvailabilityForScenePlaceholders()
    {
        var actorInProse = PromptLibraryService.CreateDefaultState();
        actorInProse.Prompts[PromptLibraryStageIds.Prose].System = "{actor.name}";
        var actorException = Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(actorInProse));
        Assert.Contains("not available", actorException.Message, StringComparison.Ordinal);

        var requestedTurnShapeInProse = PromptLibraryService.CreateDefaultState();
        requestedTurnShapeInProse.Prompts[PromptLibraryStageIds.Prose].User = "{requestedTurnShapeSection}";
        var turnShapeException = Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(requestedTurnShapeInProse));
        Assert.Contains("not available", turnShapeException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsNestedAndUnclosedComments()
    {
        var nested = PromptLibraryService.CreateDefaultState();
        nested.Prompts[PromptLibraryStageIds.Planning].System = "/* one /* two */ */";
        Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(nested));

        var unclosed = PromptLibraryService.CreateDefaultState();
        unclosed.Prompts[PromptLibraryStageIds.Planning].System = "/* one";
        Assert.Throws<InvalidOperationException>(() => PromptLibraryService.ValidateState(unclosed));
    }

    [Fact]
    public void NormalizePreservesCustomValuesAndFillsMissingDefaults()
    {
        var partial = new PromptLibraryState
        {
            Prompts = new()
            {
                [PromptLibraryStageIds.Planning] = new() { System = "Custom system", User = "Custom user" }
            },
            TurnShapes = new()
            {
                [PromptLibraryStageIds.Prose] =
                [
                    new() { Id = "brief", Label = "Brief", Value = "Custom brief" }
                ]
            }
        };

        var normalized = PromptLibraryService.NormalizeState(partial);

        Assert.Equal("Custom system", normalized.Prompts[PromptLibraryStageIds.Planning].System);
        Assert.Contains("You update character scene state.", normalized.Prompts[PromptLibraryStageIds.Appearance].System, StringComparison.Ordinal);
        Assert.Equal("Custom brief", normalized.TurnShapes[PromptLibraryStageIds.Prose].First(shape => shape.Id == "brief").Value);
        Assert.Contains(normalized.TurnShapes[PromptLibraryStageIds.Prose], shape => shape.Id == "silent-monologue");
    }

    static string Shape(PromptLibraryState state, string stageId, string id) =>
        state.TurnShapes[stageId].First(shape => shape.Id == id).Value;

    static void AssertPromptEqual(string expected, string actual) =>
        Assert.Equal(NormalizeNewlines(expected), NormalizeNewlines(actual));

    static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    static string AgentRp1Path(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, "AgentRp1", "AgentRp", .. parts]);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return Path.Combine(["W:\\AgentRp1\\AgentRp", .. parts]);
    }

    static string ExtractRawStringAssignedTo(string path, string name)
    {
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(name, StringComparison.Ordinal) || !lines[i].Contains("=", StringComparison.Ordinal))
                continue;

            return ExtractRawStringAt(lines, i);
        }

        throw new InvalidOperationException($"Could not find '{name}' in '{path}'.");
    }

    static string ExtractFirstRawStringAfter(string path, string marker)
    {
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(marker, StringComparison.Ordinal))
                return ExtractRawStringAt(lines, i);
        }

        throw new InvalidOperationException($"Could not find '{marker}' in '{path}'.");
    }

    static string ExtractRawStringAt(string[] lines, int startAt)
    {
        var open = startAt;
        while (open < lines.Length && lines[open].Trim() != "\"\"\"")
            open++;

        var close = open + 1;
        while (close < lines.Length && !lines[close].TrimStart().StartsWith("\"\"\"", StringComparison.Ordinal))
            close++;

        var indent = lines[close][..(lines[close].Length - lines[close].TrimStart().Length)];
        var content = lines[(open + 1)..close]
            .Select(line => line.StartsWith(indent, StringComparison.Ordinal) ? line[indent.Length..] : line)
            .ToArray();
        return string.Join(Environment.NewLine, content).TrimEnd();
    }

    const string ExpectedSnapshotUserPromptTemplate =
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
        2. Proposed facts that should be canonized.
        3. Proposed timeline entries that should be added.
        For characterNames, locationNames, and itemNames, only use names from the provided catalogs.
        """;

    const string ExpectedPlanningTurnShapeDefinitions =
        """
        - compact = one action beat, one or two phrases, optional short tag (always preferred)
        - silent = quick action/subtext only, no spoken lines (common)
        - silent monologue = extended action/subtext only, no spoken lines; detailed movement, touch, posture, expression, atmosphere, or implication across one playable move (common in intimate, physical, or subtext-heavy moments)
        - brief = one action beat, one to two short lines with a tag in between (rare)
        - extended = elaborate the beat into three focused paragraphs with well choreography interactions (only when asked)
        - monologue = short monologue allowed (only when asked)
        """;

    const string ExpectedProseBriefTurnShape =
        """
        Write only a very brief turn on the same line with:
        - One brief *action*.
        - One or two short "spoken lines" separated by simple *action*.
        """;

    const string ExpectedProseSilentMonologueTurnShape =
        """
        Write only a silent monologue turn with:
        - Detailed nonverbal *action* and subtext only.
        - Use touch, movement, posture, expression, distance, hesitation, or atmosphere.
        - Build one connected physical move with a clear landing point.
        - Do not use "dialogue" or explain the subtext directly.
        - Stop before it becomes a sequence or exposition.
        """;
}
