using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class PromptLibraryServiceTests
{
    [Fact]
    public void DefaultStateIncludesRequiredEditableStages()
    {
        var defaults = PromptLibraryService.CreateDefaultState();
        var stageIds = PromptLibraryService.GetStageDefinitions().Select(stage => stage.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(stageIds, stageId => Assert.True(defaults.Prompts.ContainsKey(stageId), $"Missing default prompt for {stageId}."));
        Assert.Contains(PromptLibraryStageIds.SceneContinuity, stageIds);
        Assert.Contains(PromptLibraryStageIds.Selection, stageIds);
        Assert.Contains(PromptLibraryStageIds.Planning, stageIds);
        Assert.Contains(PromptLibraryStageIds.Prose, stageIds);
        Assert.Contains(PromptLibraryStageIds.Snapshot, stageIds);
        Assert.EndsWith(PromptLibraryService.ProseFormatReminder, defaults.Prompts[PromptLibraryStageIds.Prose].User, StringComparison.Ordinal);
        Assert.DoesNotContain("silent monologue", string.Join("\n", defaults.TurnShapes.SelectMany(pair => pair.Value).Select(shape => shape.Value)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestedTurnShapePromptIncludesOnlySelectedShapeDefinition()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        var section = PromptLibraryService.FormatRequestedTurnShape(defaults, PromptLibraryStageIds.Planning, "Silent Extended");

        Assert.Contains("Required turn shape: Silent Extended", section, StringComparison.Ordinal);
        Assert.Contains("- silent extended =", section, StringComparison.Ordinal);
        Assert.DoesNotContain("brief =", section, StringComparison.Ordinal);
        Assert.DoesNotContain("narrative =", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoRequestedTurnShapePromptIncludesAllShapeDefinitions()
    {
        var defaults = PromptLibraryService.CreateDefaultState();

        var section = PromptLibraryService.FormatRequestedTurnShape(defaults, PromptLibraryStageIds.Planning, "Auto");

        Assert.Contains("Choose the turn shape that best fits this turn.", section, StringComparison.Ordinal);
        Assert.Contains("Turn shape definitions:", section, StringComparison.Ordinal);
        Assert.Contains("- compact =", section, StringComparison.Ordinal);
        Assert.Contains("- narrative =", section, StringComparison.Ordinal);
        Assert.DoesNotContain("Required turn shape:", section, StringComparison.Ordinal);
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

        Assert.False(string.IsNullOrWhiteSpace(defaults.Prompts[PromptLibraryStageIds.StoryAssistantBase].System));
        Assert.False(string.IsNullOrWhiteSpace(defaults.Prompts[PromptLibraryStageIds.StoryAssistantPrepareStory].User));
        Assert.False(string.IsNullOrWhiteSpace(defaults.Prompts[PromptLibraryStageIds.StoryAssistantIntroduceCharacters].User));
        Assert.False(string.IsNullOrWhiteSpace(defaults.Prompts[PromptLibraryStageIds.StoryAssistantIntroduceLocation].User));
        Assert.False(string.IsNullOrWhiteSpace(defaults.Prompts[PromptLibraryStageIds.StoryAssistantChangeScene].User));
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
        Assert.False(string.IsNullOrWhiteSpace(normalized.Prompts[PromptLibraryStageIds.StoryAssistantBase].System));
    }
}
