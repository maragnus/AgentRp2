using AgentRp.Components.Common;
using Microsoft.AspNetCore.Components.Forms;

namespace AgentRp.Tests;

public sealed class StatefulFormContextTests
{
    [Fact]
    public void HasChangesReflectsRealBaselineDifference()
    {
        var model = new TestDraft { Title = "Saved" };
        using var context = CreateContext(model);

        Assert.False(context.HasChanges);

        model.Title = "Changed";

        Assert.True(context.HasChanges);

        model.Title = "Saved";

        Assert.False(context.HasChanges);
    }

    [Fact]
    public async Task SaveResetsBaseline()
    {
        var saved = false;
        var model = new TestDraft { Title = "Saved" };
        using var context = new StatefulFormContext<TestDraft>(
            model,
            StatefulFormSnapshot.Clone(model),
            () =>
            {
                saved = true;
                return Task.CompletedTask;
            },
            () => Task.CompletedTask,
            () => { });

        model.Title = "Changed";

        await context.SaveAsync();

        Assert.True(saved);
        Assert.False(context.HasChanges);
    }

    [Fact]
    public async Task GuardRunsActionOnlyAfterAbandon()
    {
        var actionRan = false;
        var model = new TestDraft { Title = "Saved" };
        using var context = CreateContext(model);

        model.Title = "Changed";

        await context.GuardAsync(() =>
        {
            actionRan = true;
            return Task.CompletedTask;
        });

        Assert.True(context.ShowUnsavedChangesDialog);
        Assert.False(actionRan);

        await context.AbandonAndContinueAsync();

        Assert.True(actionRan);
        Assert.False(context.ShowUnsavedChangesDialog);
    }

    [Fact]
    public async Task GuardSavesBeforeContinuing()
    {
        var actionRan = false;
        var saved = false;
        var model = new TestDraft { Title = "Saved" };
        using var context = new StatefulFormContext<TestDraft>(
            model,
            StatefulFormSnapshot.Clone(model),
            () =>
            {
                saved = true;
                return Task.CompletedTask;
            },
            () => Task.CompletedTask,
            () => { });

        model.Title = "Changed";
        await context.GuardAsync(() =>
        {
            actionRan = true;
            return Task.CompletedTask;
        });
        await context.ConfirmSaveAndContinueAsync();

        Assert.True(saved);
        Assert.True(actionRan);
        Assert.False(context.HasChanges);
    }

    [Fact]
    public void PathDirtyReflectsBaselineDifference()
    {
        var model = new TestDraft { Title = "Saved" };
        using var context = CreateContext(model);

        model.Title = "Changed";

        Assert.True(context.IsPathDirty(nameof(TestDraft.Title)));
    }

    [Fact]
    public void PathDirtyClearsWhenValueReturnsToBaseline()
    {
        var model = new TestDraft { Title = "Saved" };
        using var context = CreateContext(model);

        model.Title = "Changed";
        model.Title = "Saved";

        Assert.False(context.IsPathDirty(nameof(TestDraft.Title)));
    }

    [Fact]
    public void NestedPathDirtyReflectsBaselineDifference()
    {
        var model = new TestDraft { Child = new() { Name = "Saved" } };
        using var context = CreateContext(model);

        model.Child.Name = "Changed";

        Assert.True(context.IsPathDirty("Child.Name"));
    }

    [Fact]
    public void CollectionPathDirtyReflectsBaselineDifference()
    {
        var model = new TestDraft { Tags = ["saved"] };
        using var context = CreateContext(model);

        model.Tags.Add("changed");

        Assert.True(context.IsPathDirty(nameof(TestDraft.Tags)));
    }

    [Fact]
    public void InvalidPathThrows()
    {
        var model = new TestDraft { Title = "Saved" };
        using var context = CreateContext(model);

        Assert.Throws<InvalidOperationException>(() => context.IsPathDirty("Missing"));
    }

    [Fact]
    public void ScopeDirtyUsesRegisteredPredicate()
    {
        var model = new TestDraft { Title = "Saved" };
        using var context = CreateContext(model);
        using var registration = context.RegisterScope("custom", () => true);

        Assert.True(context.IsScopeDirty("custom"));
    }

    [Fact]
    public void FieldDirtyReflectsBaselineDifferenceNotTouchedState()
    {
        var model = new TestDraft { Title = "Saved" };
        using var context = CreateContext(model);
        var field = new FieldIdentifier(model, nameof(TestDraft.Title));

        model.Title = "Changed";

        Assert.True(context.IsFieldDirty(field));

        model.Title = "Saved";

        Assert.False(context.IsFieldDirty(field));
    }

    static StatefulFormContext<TestDraft> CreateContext(TestDraft model) => new(
        model,
        StatefulFormSnapshot.Clone(model),
        () => Task.CompletedTask,
        () => Task.CompletedTask,
        () => { });

    sealed class TestDraft
    {
        public string Title { get; set; } = "";
        public TestChild Child { get; set; } = new();
        public List<string> Tags { get; set; } = [];
    }

    sealed class TestChild
    {
        public string Name { get; set; } = "";
    }
}
