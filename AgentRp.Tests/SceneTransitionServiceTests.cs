using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class SceneTransitionServiceTests
{
    [Fact]
    public void OpeningSceneBuildsTargetSceneAndInstruction()
    {
        var document = CreateDocument();

        var plan = new SceneTransitionService().Build(document, new("l1", ["c1"], [], Guidance(SceneNarratorGuidancePurpose.OpeningScene, "Start here.")));

        Assert.True(plan.IsOpeningScene);
        Assert.False(plan.IsLocationTransition);
        Assert.Equal("l1", plan.TargetScene.LocationId);
        Assert.Equal(["c1"], plan.TargetScene.InSceneCharacterIds);
        Assert.Empty(plan.TargetScene.InSceneItemIds);
        Assert.Contains("Scene setting purpose: opening scene", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("Narrator guidance: Start here.", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("This is the opening scene", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains(PromptLibraryService.NarratorWardrobeGuidance, plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("Do not create the next playable character beat", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("End with the scene staged so a character can react next", plan.NarratorInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void LocationTransitionComputesCharacterAndItemDeltas()
    {
        var document = CreateDocument();
        document.Transcript.RootScene = new()
        {
            LocationId = "l1",
            LocationName = "Apartment",
            InSceneCharacterIds = ["c1", "c2"],
            InSceneItemIds = ["i1"]
        };
        document.Transcript.Turns.Add(new()
        {
            Id = "turn-1",
            Scene = document.Transcript.RootScene
        });
        document.Transcript.ActiveLeafTurnId = "turn-1";

        var plan = new SceneTransitionService().Build(document, new("l2", ["c2", "c3"], ["i2"], Guidance(SceneNarratorGuidancePurpose.LocationTransition, "Move quietly.")));

        Assert.False(plan.IsOpeningScene);
        Assert.True(plan.IsLocationTransition);
        Assert.Equal(["Mara"], plan.AddedCharacters.Select(item => item.Name).ToList());
        Assert.Equal(["Lucia"], plan.RemovedCharacters.Select(item => item.Name).ToList());
        Assert.Equal(["Map"], plan.AddedItems.Select(item => item.Name).ToList());
        Assert.Equal(["Lantern"], plan.RemovedItems.Select(item => item.Name).ToList());
        Assert.Contains("Narrator guidance: Move quietly.", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("Scene delta:", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("Characters in scene: Gemma, Mara.", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("Note the transition to the new location", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("You may summarize already-established or user-approved offscreen continuity", plan.NarratorInstruction, StringComparison.Ordinal);
        Assert.Contains("no dialogue, internal monologue, new emotional reactions", plan.NarratorInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void SameLocationDeltaOnlyShowsEnteredAndLeftEntities()
    {
        var document = CreateDocument();
        var previous = new RpSceneFrame
        {
            LocationId = "l1",
            LocationName = "Apartment",
            InSceneCharacterIds = ["c1", "c2"],
            InSceneItemIds = ["i1"]
        };
        var target = new RpSceneFrame
        {
            LocationId = "l1",
            LocationName = "Apartment",
            InSceneCharacterIds = ["c2", "c3"],
            InSceneItemIds = ["i2"]
        };

        var service = new SceneTransitionService();
        var delta = service.BuildDelta(document, previous, target);
        var transcript = service.FormatForTranscript(delta);

        Assert.False(delta.IsLocationTransition);
        Assert.Contains("Lucia left the scene.", transcript, StringComparison.Ordinal);
        Assert.Contains("Mara entered the scene.", transcript, StringComparison.Ordinal);
        Assert.Contains("Lantern was removed from the scene.", transcript, StringComparison.Ordinal);
        Assert.Contains("Map was added to the scene.", transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("Gemma", transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("previously", transcript, StringComparison.Ordinal);
    }

    [Fact]
    public void LocationDeltaReestablishesPresentEntities()
    {
        var document = CreateDocument();
        var previous = new RpSceneFrame
        {
            LocationId = "l1",
            LocationName = "Apartment",
            InSceneCharacterIds = ["c1", "c2"],
            InSceneItemIds = ["i1"]
        };
        var target = new RpSceneFrame
        {
            LocationId = "l2",
            LocationName = "Library",
            InSceneCharacterIds = ["c2", "c3"],
            InSceneItemIds = ["i2"]
        };

        var service = new SceneTransitionService();
        var delta = service.BuildDelta(document, previous, target);
        var transcript = service.FormatForTranscript(delta);

        Assert.True(delta.IsLocationTransition);
        Assert.Contains("Lucia left the scene.", transcript, StringComparison.Ordinal);
        Assert.Contains("Library (previously Apartment).", transcript, StringComparison.Ordinal);
        Assert.Contains("Gemma and Mara are present in the scene.", transcript, StringComparison.Ordinal);
        Assert.Contains("Map is present in the scene.", transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("Mara entered the scene.", transcript, StringComparison.Ordinal);
    }

    [Fact]
    public void LocationDeltaOmitsMissingPreviousLocation()
    {
        var document = CreateDocument();
        var previous = new RpSceneFrame
        {
            LocationName = "No Location",
            InSceneCharacterIds = ["c1"]
        };
        var target = new RpSceneFrame
        {
            LocationId = "l2",
            LocationName = "Library",
            InSceneCharacterIds = ["c1"]
        };

        var transcript = new SceneTransitionService().FormatForTranscript(new SceneTransitionService().BuildDelta(document, previous, target));

        Assert.Contains("Library.", transcript, StringComparison.Ordinal);
        Assert.DoesNotContain("previously", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No Location", transcript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TimeSkipWithoutLocationChangeIsClassified()
    {
        var document = CreateDocument();
        document.Transcript.RootScene = new()
        {
            LocationId = "l1",
            LocationName = "Apartment",
            InSceneCharacterIds = ["c1"]
        };
        document.Transcript.Turns.Add(new()
        {
            Id = "turn-1",
            Scene = document.Transcript.RootScene
        });
        document.Transcript.ActiveLeafTurnId = "turn-1";

        var plan = new SceneTransitionService().Build(document, new("l1", ["c1"], [], Guidance(SceneNarratorGuidancePurpose.TimeSkip, "Two hours later.")));

        Assert.False(plan.IsLocationTransition);
        Assert.True(plan.IsTimeSkip);
        Assert.Contains("Two hours later", plan.NarratorInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownIdsFailWithClearReason()
    {
        var document = CreateDocument();

        var exception = Assert.Throws<SceneTransitionValidationException>(() =>
            new SceneTransitionService().Build(document, new("l1", ["missing"], [], Guidance())));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateIdsAreNormalized()
    {
        var document = CreateDocument();

        var plan = new SceneTransitionService().Build(document, new("l1", ["c1", "c1"], ["i1", "i1"], Guidance()));

        Assert.Equal(["c1"], plan.TargetScene.InSceneCharacterIds);
        Assert.Equal(["i1"], plan.TargetScene.InSceneItemIds);
    }

    static RpChatDocument CreateDocument() => new()
    {
        Chat = new() { Id = "ch1" },
        Locations =
        [
            new() { Id = "l1", Name = "Apartment" },
            new() { Id = "l2", Name = "Library" }
        ],
        Characters =
        [
            new() { Id = "c1", Name = "Lucia" },
            new() { Id = "c2", Name = "Gemma" },
            new() { Id = "c3", Name = "Mara" }
        ],
        Items =
        [
            new() { Id = "i1", Name = "Lantern" },
            new() { Id = "i2", Name = "Map" }
        ]
    };

    static SceneNarratorGuidance Guidance(SceneNarratorGuidancePurpose purpose = SceneNarratorGuidancePurpose.SceneReset, string text = "Set the scene.") =>
        new(purpose, text);
}
