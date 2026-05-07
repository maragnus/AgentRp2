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

        var plan = new SceneTransitionService().Build(document, new("l1", ["c1"], [], Reason: "Start here."));

        Assert.True(plan.IsOpeningScene);
        Assert.False(plan.IsLocationTransition);
        Assert.Equal("l1", plan.TargetScene.LocationId);
        Assert.Equal(["c1"], plan.TargetScene.InSceneCharacterIds);
        Assert.Empty(plan.TargetScene.InSceneItemIds);
        Assert.Contains("This is the opening scene", plan.NarratorInstruction, StringComparison.Ordinal);
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

        var plan = new SceneTransitionService().Build(document, new("l2", ["c2", "c3"], ["i2"], TransitionNote: "Move quietly."));

        Assert.False(plan.IsOpeningScene);
        Assert.True(plan.IsLocationTransition);
        Assert.Equal(["Mara"], plan.AddedCharacters.Select(item => item.Name).ToList());
        Assert.Equal(["Lucia"], plan.RemovedCharacters.Select(item => item.Name).ToList());
        Assert.Equal(["Gemma"], plan.StayingCharacters.Select(item => item.Name).ToList());
        Assert.Equal(["Map"], plan.AddedItems.Select(item => item.Name).ToList());
        Assert.Equal(["Lantern"], plan.RemovedItems.Select(item => item.Name).ToList());
        Assert.Contains("Note the transition to the new location", plan.NarratorInstruction, StringComparison.Ordinal);
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

        var plan = new SceneTransitionService().Build(document, new("l1", ["c1"], [], ElapsedTime: "Two hours later"));

        Assert.False(plan.IsLocationTransition);
        Assert.True(plan.IsTimeSkip);
        Assert.Contains("Two hours later", plan.NarratorInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownIdsFailWithClearReason()
    {
        var document = CreateDocument();

        var exception = Assert.Throws<SceneTransitionValidationException>(() =>
            new SceneTransitionService().Build(document, new("l1", ["missing"], [])));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateIdsAreNormalized()
    {
        var document = CreateDocument();

        var plan = new SceneTransitionService().Build(document, new("l1", ["c1", "c1"], ["i1", "i1"]));

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
}
