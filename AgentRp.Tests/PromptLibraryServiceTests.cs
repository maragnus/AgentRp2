using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class PromptLibraryServiceTests
{
    [Fact]
    public void DefaultsMatchAgentRp1PromptLibraryContentWithAgentRp2ProseFormatReminderExceptTurnShapeVocabulary()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        Assert.Contains("You update scene continuity state.", defaults.Prompts[PromptLibraryStageIds.SceneContinuity].System, StringComparison.Ordinal);
        Assert.Contains("Prior physical/body/object scene ledger", defaults.Prompts[PromptLibraryStageIds.SceneContinuity].User, StringComparison.Ordinal);
        AssertPromptEqual(ExtractFirstRawStringAfter(AgentRp1Path("Services", "StoryScenePrompts", "StorySceneResponderSelectionPromptBuilder.cs"), "BuildSystemPrompt"), defaults.Prompts[PromptLibraryStageIds.Selection].System);
        AssertPromptEqual(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultSelectionUserPromptTemplate"), defaults.Prompts[PromptLibraryStageIds.Selection].User);
        AssertPromptEqual(ExtractRawStringAssignedTo(AgentRp1Path("Services", "StoryScenePromptLibraryService.cs"), "DefaultPlanningUserPromptTemplate"), defaults.Prompts[PromptLibraryStageIds.Planning].User);
        Assert.Contains("Notice the newest meaningful change", defaults.Prompts[PromptLibraryStageIds.Prose].System, StringComparison.Ordinal);
        Assert.Contains("Write the turn by using the latest meaningful change", defaults.Prompts[PromptLibraryStageIds.Prose].User, StringComparison.Ordinal);
        Assert.EndsWith(PromptLibraryService.ProseFormatReminder, defaults.Prompts[PromptLibraryStageIds.Prose].User, StringComparison.Ordinal);
        AssertPromptEqual(ExpectedSnapshotSystemPrompt, defaults.Prompts[PromptLibraryStageIds.Snapshot].System);
        AssertPromptEqual(ExpectedSnapshotUserPromptTemplate, defaults.Prompts[PromptLibraryStageIds.Snapshot].User);
        Assert.Contains("Turn shape: copy the required turn shape exactly when one is provided", defaults.Prompts[PromptLibraryStageIds.Planning].System, StringComparison.Ordinal);
        Assert.DoesNotContain("{planning.turnShapeDefinitions}", defaults.Prompts[PromptLibraryStageIds.Planning].System, StringComparison.Ordinal);
        Assert.DoesNotContain("Prioritize compact", defaults.Prompts[PromptLibraryStageIds.Planning].System, StringComparison.Ordinal);
        Assert.DoesNotContain("silent monologue", defaults.Prompts[PromptLibraryStageIds.Planning].System, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TurnShapeDefaultsUseCanonicalDefinitions()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        Assert.Equal("one action beat, one or two phrases, optional short tag (always preferred)", Shape(defaults, PromptLibraryStageIds.Planning, "compact"));
        Assert.Equal("one action beat, one to two short lines with a tag in between (rare)", Shape(defaults, PromptLibraryStageIds.Planning, "brief"));
        Assert.Equal("short narrative allowed (only when asked)", Shape(defaults, PromptLibraryStageIds.Planning, "extended"));
        Assert.Equal("elaborate the beat into three focused paragraphs with well-choreographed interactions (only when asked)", Shape(defaults, PromptLibraryStageIds.Planning, "narrative"));
        Assert.Equal("quick action/subtext only, no spoken lines (common)", Shape(defaults, PromptLibraryStageIds.Planning, "silent"));
        Assert.Equal("extended action/subtext only, no spoken lines; detailed movement, touch, posture, expression, atmosphere, or implication across one playable move (very rare, used to close out intimate, physical, or subtext-heavy moments)", Shape(defaults, PromptLibraryStageIds.Planning, "silent-extended"));
        AssertPromptEqual(ExpectedPlanningTurnShapeDefinitions, PromptLibraryService.FormatTurnShapeDefinitions(defaults.TurnShapes[PromptLibraryStageIds.Planning]));
        AssertPromptEqual(ExpectedProseBriefTurnShape, Shape(defaults, PromptLibraryStageIds.Prose, "brief"));
        AssertPromptEqual(ExpectedProseSilentExtendedTurnShape, Shape(defaults, PromptLibraryStageIds.Prose, "silent-extended"));
    }

    [Fact]
    public void RequestedTurnShapePromptIncludesOnlySelectedShapeDefinition()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        var section = PromptLibraryService.FormatRequestedTurnShape(defaults, PromptLibraryStageIds.Planning, "Silent Extended");

        AssertPromptEqual(ExpectedSilentExtendedRequestedTurnShape, section);
        Assert.DoesNotContain("brief =", section, StringComparison.Ordinal);
        Assert.DoesNotContain("narrative =", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoRequestedTurnShapePromptIncludesAllShapeDefinitions()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        var section = PromptLibraryService.FormatRequestedTurnShape(defaults, PromptLibraryStageIds.Planning, "Auto");

        AssertPromptEqual(ExpectedAutoRequestedTurnShape, section);
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
        Assert.Contains("You update scene continuity state.", normalized.Prompts[PromptLibraryStageIds.SceneContinuity].System, StringComparison.Ordinal);
        Assert.Equal("Custom brief", normalized.TurnShapes[PromptLibraryStageIds.Prose].First(shape => shape.Id == "brief").Value);
        Assert.Contains(normalized.TurnShapes[PromptLibraryStageIds.Prose], shape => shape.Id == "silent-extended");
    }

    [Fact]
    public void OverridesApplyOnlyChangedPromptFields()
    {
        var state = PromptLibraryService.CreateOverrideState();
        state.PromptOverrides[PromptLibraryStageIds.Planning] = new()
        {
            System = "Custom system"
        };

        var normalized = PromptLibraryService.NormalizeState(state);
        var defaults = PromptLibraryService.CreateDefaultState();

        Assert.Equal("Custom system", normalized.Prompts[PromptLibraryStageIds.Planning].System);
        Assert.Equal(defaults.Prompts[PromptLibraryStageIds.Planning].User, normalized.Prompts[PromptLibraryStageIds.Planning].User);
    }

    [Fact]
    public void CreateOverridesFromResolvedDropsValuesThatMatchDefaults()
    {
        var resolved = PromptLibraryService.CreateDefaultState();
        resolved.Prompts[PromptLibraryStageIds.Planning].System = "Custom planning system";
        resolved.TurnShapes[PromptLibraryStageIds.Prose].First(shape => shape.Id == "brief").Value = "Custom brief";

        var overrides = PromptLibraryService.CreateOverridesFromResolved(resolved);

        Assert.Empty(overrides.Prompts);
        Assert.Empty(overrides.TurnShapes);
        Assert.True(overrides.PromptOverrides.ContainsKey(PromptLibraryStageIds.Planning));
        Assert.Equal("Custom planning system", overrides.PromptOverrides[PromptLibraryStageIds.Planning].System);
        Assert.Null(overrides.PromptOverrides[PromptLibraryStageIds.Planning].User);
        Assert.Single(overrides.TurnShapeOverrides[PromptLibraryStageIds.Prose]);
        Assert.Equal("brief", overrides.TurnShapeOverrides[PromptLibraryStageIds.Prose][0].Id);
        Assert.Equal("Custom brief", overrides.TurnShapeOverrides[PromptLibraryStageIds.Prose][0].Value);
    }

    [Fact]
    public void CreateOverridesFromResolvedReturnsEmptyStateAfterResetToDefaults()
    {
        var resolved = PromptLibraryService.CreateDefaultState();

        var overrides = PromptLibraryService.CreateOverridesFromResolved(resolved);

        Assert.Empty(overrides.PromptOverrides);
        Assert.Empty(overrides.TurnShapeOverrides);
    }

    [Fact]
    public void NormalizeDropsLegacyTurnShapeRows()
    {
        var partial = new PromptLibraryState
        {
            TurnShapes = new()
            {
                [PromptLibraryStageIds.Prose] =
                [
                    new() { Id = "monologue", Label = "Monologue", Value = "Legacy monologue" },
                    new() { Id = "silent-monologue", Label = "Silent Monologue", Value = "Legacy silent monologue" },
                    new() { Id = "narrative", Label = "Narrative", Value = "Custom narrative" }
                ]
            }
        };

        var normalized = PromptLibraryService.NormalizeState(partial);
        var proseShapes = normalized.TurnShapes[PromptLibraryStageIds.Prose];

        Assert.DoesNotContain(proseShapes, shape => shape.Id == "monologue");
        Assert.DoesNotContain(proseShapes, shape => shape.Id == "silent-monologue");
        Assert.Equal("Custom narrative", proseShapes.First(shape => shape.Id == "narrative").Value);
    }

    [Fact]
    public void DefaultsIncludeEditableStoryAssistantWorkflowStages()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        Assert.Contains("You are a friendly Story Entities Assistant", defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
        Assert.Contains("Always use the `ask_user` tool whenever you need the user to answer", defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
        Assert.Contains("flat appearance fields", defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
        Assert.Contains("complete visual profile", defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
        Assert.Contains("extraAppearanceDetails", defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
        Assert.Contains("relationshipReconciliation", defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
        Assert.Contains("Never leave any relationship field empty", defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
        Assert.Contains("Prepare a New Story", defaults.Prompts[PromptLibraryStageIds.StoryAssistantPrepareStory].User, StringComparison.Ordinal);
        Assert.Contains("Do not begin with a prose questionnaire", defaults.Prompts[PromptLibraryStageIds.StoryAssistantPrepareStory].User, StringComparison.Ordinal);
        Assert.Contains("Introduce Characters", defaults.Prompts[PromptLibraryStageIds.StoryAssistantIntroduceCharacters].User, StringComparison.Ordinal);
        Assert.Contains("Introduce a Location", defaults.Prompts[PromptLibraryStageIds.StoryAssistantIntroduceLocation].User, StringComparison.Ordinal);
        Assert.Contains("Change the Scene", defaults.Prompts[PromptLibraryStageIds.StoryAssistantChangeScene].User, StringComparison.Ordinal);
        Assert.All(
            PromptLibraryService.GetStageDefinitions().Where(stage => stage.Id.StartsWith("storyAssistant", StringComparison.Ordinal)),
            stage => Assert.Equal(PromptLibraryStageGroups.StoryAssistant, stage.Group));
    }

    [Fact]
    public void NormalizePreservesCustomStoryAssistantWorkflowPrompt()
    {
        var partial = new PromptLibraryState
        {
            Prompts = new()
            {
                [PromptLibraryStageIds.StoryAssistantChangeScene] = new() { System = "", User = "Custom change scene guidance." }
            }
        };

        var normalized = PromptLibraryService.NormalizeState(partial);

        Assert.Equal("Custom change scene guidance.", normalized.Prompts[PromptLibraryStageIds.StoryAssistantChangeScene].User);
        Assert.Contains("You are a friendly Story Entities Assistant", normalized.Prompts[PromptLibraryStageIds.StoryAssistantBase].System, StringComparison.Ordinal);
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
        2. Proposed timeline entries that should be added, with numeric turnNumber, title, and description.
        For characterNames, locationNames, and itemNames, only use names from the provided catalogs.
        """;

    const string ExpectedSnapshotSystemPrompt =
        """
        You create structured story snapshots from a selected branch transcript.
        Summarize only what is supported by the included messages and supplied story state.
        Return a concise narrative summary, then propose timeline entries that should be saved.
        Prefer durable developments over throwaway phrasing.
        Do not invent names, references, or events that are not grounded in the provided material.
        Timeline entry turnNumber must be the best matching included numeric turn number, such as 52, and never a text label or real-world date.
        If an event spans multiple turns, use the turn where the event becomes canon or settled.
        """;

    const string ExpectedPlanningTurnShapeDefinitions =
        """
        - compact = one action beat, one or two phrases, optional short tag (always preferred)
        - silent = quick action/subtext only, no spoken lines (common)
        - silent extended = extended action/subtext only, no spoken lines; detailed movement, touch, posture, expression, atmosphere, or implication across one playable move (very rare, used to close out intimate, physical, or subtext-heavy moments)
        - brief = one action beat, one to two short lines with a tag in between (rare)
        - extended = short narrative allowed (only when asked)
        - narrative = elaborate the beat into three focused paragraphs with well-choreographed interactions (only when asked)
        """;

    const string ExpectedProseBriefTurnShape =
        """
        Write only a very brief turn on the same line with:
        - One brief *action*.
        - One or two short "spoken lines" separated by simple *action*.
        """;

    const string ExpectedSilentExtendedRequestedTurnShape =
        """
        Required turn shape: Silent Extended
        Use exactly this turn shape in the structured plan.
        Turn shape definition:
        - silent extended = extended action/subtext only, no spoken lines; detailed movement, touch, posture, expression, atmosphere, or implication across one playable move (very rare, used to close out intimate, physical, or subtext-heavy moments)
        """;

    const string ExpectedAutoRequestedTurnShape =
        """
        Choose the turn shape that best fits this turn.
        Use one of these turn shapes exactly in the structured plan.
        Turn shape definitions:
        - compact = one action beat, one or two phrases, optional short tag (always preferred)
        - silent = quick action/subtext only, no spoken lines (common)
        - silent extended = extended action/subtext only, no spoken lines; detailed movement, touch, posture, expression, atmosphere, or implication across one playable move (very rare, used to close out intimate, physical, or subtext-heavy moments)
        - brief = one action beat, one to two short lines with a tag in between (rare)
        - extended = short narrative allowed (only when asked)
        - narrative = elaborate the beat into three focused paragraphs with well-choreographed interactions (only when asked)

        Prioritize compact, brief, or silent almost always.
        - Favor silent turns for quick intimate moments.
        - Don't eagerly follow the narrative if it is counter to character goals or private intent.
        - Pick the most valuable next beat to move the story forward, not the safest or most literal reply.
        - Identify when the current thread has run its course and move on.
        - If a direct reaction is needed, react.
        - If no direct reaction is needed, introduce a small new beat that moves the scene.
        - Never end an exchange.
        - Never end a conversation.
        """;

    const string ExpectedProseSilentExtendedTurnShape =
        """
        Write only a silent extended turn with:
        - Detailed nonverbal *action* and subtext only.
        - Use touch, movement, posture, expression, distance, hesitation, or atmosphere.
        - Build one connected physical move with a clear landing point.
        - Do not use "dialogue" or explain the subtext directly.
        - Stop before it becomes a sequence or exposition.
        """;
}
