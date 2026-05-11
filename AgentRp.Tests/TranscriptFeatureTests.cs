using AgentRp.Components.Chat;
using AgentRp.Components.Common;
using AgentRp.Components.Entities;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace AgentRp.Tests;

public sealed class TranscriptFeatureTests
{
    [Fact]
    public async Task EditTurnUpdatesExistingMessageWithoutCreatingBranch()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var turnToEdit = session.Chat.Transcript.Items[1];
        await session.Chat.Transcript.EditTurnAsync(turnToEdit.Id, "Alternate Bella branch.");

        var editedTurn = session.Chat.Transcript.Items[1];
        Assert.Equal(turnToEdit.Id, editedTurn.Id);
        Assert.Equal("Alternate Bella branch.", editedTurn.Body);
        Assert.Single(session.Chat.Transcript.SiblingsFor(editedTurn.Id));
    }

    [Fact]
    public async Task EditTurnUpdatesExistingMessageAcrossSessions()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var sessionA = NewSession(liveStore, generator);
        var sessionB = NewSession(liveStore, generator);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var originalTurn = sessionA.Chat.Transcript.Items[1];
        await sessionA.Chat.Transcript.EditTurnAsync(originalTurn.Id, "Alternate Bella branch.");

        Assert.Equal(originalTurn.Id, sessionA.Chat.Transcript.Items[1].Id);
        Assert.Equal("Alternate Bella branch.", sessionA.Chat.Transcript.Items[1].Body);
        Assert.Equal("Alternate Bella branch.", sessionB.Chat.Transcript.Items[1].Body);
    }

    [Fact]
    public async Task CreatingSnapshotUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var sessionA = NewSession(liveStore, generator);
        var sessionB = NewSession(liveStore, generator);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(sessionA);
        var draft = await sessionA.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        Assert.NotNull(draft);
        Assert.Null(sessionB.Chat.Transcript.SnapshotFor(activeTurnId));
        Assert.DoesNotContain(sessionB.Chat.Timeline.Items, entry => entry.Title == "Snapshot event");
        await sessionA.Chat.Transcript.CommitSnapshotDraftAsync(draft!);

        var snapshot = sessionB.Chat.Transcript.SnapshotFor(activeTurnId);
        Assert.NotNull(snapshot);
        Assert.Equal($"Snapshot for {activeTurnId}", snapshot!.Summary);
        Assert.All(sessionB.Chat.Transcript.Items.Where(turn => draft!.CoveredTurnIds.Contains(turn.Id)), turn => Assert.Equal(snapshot.Id, turn.SnapshotId));
        Assert.Equal("Take Bella's affection while reminding Jake she noticed his silence.", snapshot.PrivateIntentByCharacterId["c2"]);
        var snapshotTimelineEntry = Assert.Single(sessionB.Chat.Timeline.Items, entry => entry.SnapshotId == snapshot.Id);
        Assert.Equal("Snapshot event", snapshotTimelineEntry.Title);
        Assert.Equal("Turn 3", snapshotTimelineEntry.Date);
        Assert.Equal(["c2"], snapshotTimelineEntry.CharacterIds);
        Assert.Equal(["l1"], snapshotTimelineEntry.LocationIds);
        Assert.Equal("Snapshot system prompt.", snapshot.Trace?.Steps.Single().SystemPrompt);
        Assert.Equal("Snapshot raw output.", snapshot.Trace?.Steps.Single().RawOutput);
        var relationshipUpdate = Assert.Single(snapshot.RelationshipUpdates);
        Assert.Equal("relationship-c1-c2", relationshipUpdate.RelationshipId);
        Assert.Equal("Best friends with sharper tension.", relationshipUpdate.PublicDynamic);
    }

    [Fact]
    public async Task SnapshotTargetForLastMessageLeavesLatestCompletedMessageLive()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();
        var target = await MakeSnapshotEligibleTargetAsync(session);
        var latestTurnId = session.Chat.Transcript.Items.Last().Id;
        Assert.NotNull(target);
        Assert.NotEqual(latestTurnId, target.TargetTurnId);

        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(latestTurnId);

        Assert.NotNull(draft);
        Assert.Equal(target.TargetTurnId, draft!.TurnId);
        Assert.Equal(5, draft.CoveredTurnIds.Count);
        Assert.DoesNotContain(latestTurnId, draft.CoveredTurnIds);
    }

    [Fact]
    public async Task SnapshotUnavailableWithFiveOrFewerUnsnapshottedMessages()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();
        SnapshotDraftTarget? target;
        do
        {
            await session.Chat.Transcript.PostManualAsync($"Snapshot setup message {session.Chat.Transcript.Items.Count + 1}.", null);
            target = session.Chat.Transcript.GetSnapshotDraftTarget(session.Chat.Transcript.Items.Last().Id);
        }
        while ((target?.UnsnapshottedTurnCount ?? 0) < 5);

        var latestTurnId = session.Chat.Transcript.Items.Last().Id;
        target = session.Chat.Transcript.GetSnapshotDraftTarget(latestTurnId);

        Assert.NotNull(target);
        Assert.False(target!.CanCreate);
        Assert.False(session.Chat.Transcript.CanCreateSnapshotAt(latestTurnId));
    }

    [Fact]
    public async Task CyoaRecoveryGeneratesDecisionWithoutAutoplay()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var session = NewSession(liveStore, generator);
        await session.InitializeAsync();
        var document = session.ActiveChat.Current!;
        document.Transcript.Cyoa.Mode = RpCyoaModes.Adventure;
        document.Transcript.Cyoa.ControlledCharacterIds = [document.Characters.First().Id];
        document.Transcript.Cyoa.PendingDecision = null;

        Assert.True(session.Chat.Transcript.NeedsCyoaDecisionRecovery);

        await session.Chat.Transcript.RecoverCyoaDecisionAsync();

        Assert.NotNull(session.Chat.Transcript.CurrentCyoaDecision);
        Assert.Equal(document.Transcript.ActiveLeafTurnId, session.Chat.Transcript.CurrentCyoaDecision!.ParentTurnId);
        Assert.Equal(1, generator.CyoaDecisionCalls);
        Assert.Equal(0, generator.AutonomousCyoaTurnCalls);
    }

    [Fact]
    public async Task SnapshotTranscriptRowShowsProcessTraceWhenEnabled()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        await session.Chat.Transcript.CommitSnapshotDraftAsync(draft!);
        var snapshot = session.Chat.Transcript.SnapshotFor(activeTurnId)!;

        var component = context.Render<SnapshotTranscriptRow>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Snapshot, snapshot)
            .Add(value => value.ShowProcess, true)
            .Add(value => value.TranscriptBusy, false)
            .Add(value => value.OnOpenEntities, _ => Task.CompletedTask));

        Assert.Contains("process-row", component.Markup, StringComparison.Ordinal);
        component.Find(".process-summary").Click();
        component.Find(".process-step-head").Click();

        Assert.Contains("Snapshot system prompt.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Snapshot user prompt.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Snapshot raw output.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Snapshot structured output.", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotTranscriptRowOpensReadonlySnapshotView()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        await session.Chat.Transcript.CommitSnapshotDraftAsync(draft!);
        var snapshot = session.Chat.Transcript.SnapshotFor(activeTurnId)!;

        var component = context.Render<SnapshotTranscriptRow>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Snapshot, snapshot)
            .Add(value => value.ShowProcess, false)
            .Add(value => value.TranscriptBusy, false)
            .Add(value => value.OnOpenEntities, _ => Task.CompletedTask));

        component.Find("button[title='View snapshot']").Click();

        Assert.Contains("Snapshot Range", component.Markup, StringComparison.Ordinal);
        Assert.Contains($"Snapshot for {activeTurnId}", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Take Bella's affection while reminding Jake she noticed his silence.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Snapshot event", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Relationship Changes", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Bella / Gemma", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Best friends with sharper tension.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Covered Turns", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("process-row", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotViewModalShowsCompactEmptyStates()
    {
        using var context = new BunitContext();
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var component = context.Render<SnapshotViewModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Snapshot, new RpTranscriptSnapshot
            {
                Id = "missing-snapshot",
                CreatedUtc = DateTime.UtcNow
            })
            .Add(value => value.OnClose, () => Task.CompletedTask));

        Assert.Contains("No summary", component.Markup, StringComparison.Ordinal);
        Assert.Contains("No continuity state", component.Markup, StringComparison.Ordinal);
        Assert.Contains("No relationship changes.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("No timeline entries", component.Markup, StringComparison.Ordinal);
        Assert.Contains("No covered turns", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CharacterRelationshipSummaryUsesSharedReadonlyDisplay()
    {
        using var context = new BunitContext();
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();
        var character = session.Chat.Characters.Items.First(character => character.Id == "c1");

        var component = context.Render<CharacterRelationshipsSummary>(parameters => parameters
            .Add(value => value.Document, session.ActiveChat.Current)
            .Add(value => value.Character, character));

        Assert.Contains("Gemma", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Best friends with charged trust.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Bella sees Gemma as her best friend", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotDraftModalShowsProcessTraceBeforeSave()
    {
        using var context = new BunitContext();
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);

        var component = context.Render<SnapshotDraftModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Draft, draft)
            .Add(value => value.IsLoading, false)
            .Add(value => value.IsSaving, false)
            .Add(value => value.CloseDisabled, false)
            .Add(value => value.OnClose, () => Task.CompletedTask)
            .Add(value => value.OnSave, _ => Task.CompletedTask));

        component.Find("button[title='Process']").Click();
        component.Find(".process-step-head").Click();

        Assert.Contains("Snapshot system prompt.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Snapshot raw output.", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotDraftModalShowsRelationshipUpdatesBeforeSave()
    {
        using var context = new BunitContext();
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);

        var component = context.Render<SnapshotDraftModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Draft, draft)
            .Add(value => value.IsLoading, false)
            .Add(value => value.IsSaving, false)
            .Add(value => value.CloseDisabled, false)
            .Add(value => value.OnClose, () => Task.CompletedTask)
            .Add(value => value.OnSave, _ => Task.CompletedTask));

        component.Find("button[title='Relationships']").Click();

        Assert.Contains("Bella / Gemma", component.Markup, StringComparison.Ordinal);
        Assert.Contains("The snapshot range changes their emotional stance.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Turns 2, 3", component.Markup, StringComparison.Ordinal);
        Assert.True(component.Find("input[type='checkbox']").HasAttribute("checked"));
    }

    [Fact]
    public async Task SavingSnapshotDraftAppliesEditedRelationshipUpdate()
    {
        using var context = new BunitContext();
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        RpTranscriptSnapshotDraft? saved = null;
        var component = context.Render<SnapshotDraftModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Draft, draft)
            .Add(value => value.IsLoading, false)
            .Add(value => value.IsSaving, false)
            .Add(value => value.CloseDisabled, false)
            .Add(value => value.OnClose, () => Task.CompletedTask)
            .Add(value => value.OnSave, value =>
            {
                saved = value;
                return Task.CompletedTask;
            }));

        component.Find("button[title='Relationships']").Click();
        var fields = component.FindComponents<AppTextarea>()
            .Where(field => string.Equals(field.Instance.Class, "character-relationship-note", StringComparison.Ordinal))
            .ToList();
        await fields[0].InvokeAsync(() => fields[0].Instance.NotifyTextValueChanged("Bella now treats Gemma as trusted but changed."));
        await fields[1].InvokeAsync(() => fields[1].Instance.NotifyTextValueChanged("Gemma now sees Bella as more willing to meet the pressure."));
        await fields[2].InvokeAsync(() => fields[2].Instance.NotifyTextValueChanged("Best friends with freshly charged honesty."));
        component.FindAll("button").First(button => button.TextContent.Contains("Save Snapshot", StringComparison.Ordinal)).Click();

        Assert.NotNull(saved);
        await session.Chat.Transcript.CommitSnapshotDraftAsync(saved!);

        var relationship = CharacterRelationshipGraph.Find(session.ActiveChat.Current!, "c1", "c2")!;
        Assert.Equal("Bella now treats Gemma as trusted but changed.", relationship.NoteAtoB);
        Assert.Equal("Gemma now sees Bella as more willing to meet the pressure.", relationship.NoteBtoA);
        Assert.Equal("Best friends with freshly charged honesty.", relationship.NoteExternal);
        Assert.Equal(new[] { "Close Friend" }, relationship.Bonds);
        Assert.Equal(new[] { "Charged" }, relationship.Dynamics);
        var snapshot = session.Chat.Transcript.SnapshotFor(activeTurnId)!;
        var update = Assert.Single(snapshot.RelationshipUpdates);
        Assert.Equal("Bella now treats Gemma as trusted but changed.", update.HowSourceSeesTarget);
        Assert.Equal("Gemma now sees Bella as more willing to meet the pressure.", update.HowTargetSeesSource);
        Assert.Equal("Best friends with freshly charged honesty.", update.PublicDynamic);
    }

    [Fact]
    public async Task UncheckedSnapshotRelationshipUpdateDoesNotMutateRelationship()
    {
        using var context = new BunitContext();
        ConfigureSnapshotComponentContext(context);
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var original = CharacterRelationshipGraph.Find(session.ActiveChat.Current!, "c1", "c2")!;
        var originalSourceNote = original.NoteAtoB;
        var originalTargetNote = original.NoteBtoA;
        var originalPublicDynamic = original.NoteExternal;
        var originalBonds = original.Bonds.ToList();
        var originalDynamics = original.Dynamics.ToList();
        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        RpTranscriptSnapshotDraft? saved = null;
        var component = context.Render<SnapshotDraftModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.Draft, draft)
            .Add(value => value.IsLoading, false)
            .Add(value => value.IsSaving, false)
            .Add(value => value.CloseDisabled, false)
            .Add(value => value.OnClose, () => Task.CompletedTask)
            .Add(value => value.OnSave, value =>
            {
                saved = value;
                return Task.CompletedTask;
            }));

        component.Find("button[title='Relationships']").Click();
        await component.Find("input[type='checkbox']").ChangeAsync(false);
        component.FindAll("button").First(button => button.TextContent.Contains("Save Snapshot", StringComparison.Ordinal)).Click();

        Assert.NotNull(saved);
        Assert.False(saved!.RelationshipUpdates.Single().ApplyChange);
        await session.Chat.Transcript.CommitSnapshotDraftAsync(saved);

        var relationship = CharacterRelationshipGraph.Find(session.ActiveChat.Current!, "c1", "c2")!;
        Assert.Equal(originalSourceNote, relationship.NoteAtoB);
        Assert.Equal(originalTargetNote, relationship.NoteBtoA);
        Assert.Equal(originalPublicDynamic, relationship.NoteExternal);
        Assert.Equal(originalBonds, relationship.Bonds);
        Assert.Equal(originalDynamics, relationship.Dynamics);
        var snapshot = session.Chat.Transcript.SnapshotFor(activeTurnId)!;
        Assert.Empty(snapshot.RelationshipUpdates);
    }

    [Fact]
    public async Task SnapshotRelationshipUpdatesPersistAcrossReload()
    {
        var persistence = new SeedRoleplayPersistence();
        await using var liveStore = new LiveRoleplayStore(persistence, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        await session.Chat.Transcript.CommitSnapshotDraftAsync(draft!);

        var reloaded = await persistence.LoadChatDocumentAsync(session.ActiveChat.Current!.Chat.Id);
        var snapshot = reloaded.Transcript.Snapshots.Single(snapshot => snapshot.TurnId == activeTurnId);
        var update = Assert.Single(snapshot.RelationshipUpdates);
        Assert.Equal("relationship-c1-c2", update.RelationshipId);
        Assert.Equal("Best friends with sharper tension.", update.PublicDynamic);
    }

    [Fact]
    public async Task UnwrappingSnapshotKeepsMessagesAndTimelineEntries()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        await session.Chat.Transcript.CommitSnapshotDraftAsync(draft!);
        var snapshot = session.Chat.Transcript.SnapshotFor(activeTurnId)!;
        var coveredTurnIds = draft!.CoveredTurnIds.ToHashSet(StringComparer.Ordinal);

        await session.Chat.Transcript.DeleteSnapshotAsync(snapshot.Id, SnapshotDeleteMethod.Unwrap, false);

        Assert.Null(session.Chat.Transcript.SnapshotFor(activeTurnId));
        Assert.All(session.Chat.Transcript.Items.Where(turn => coveredTurnIds.Contains(turn.Id)), turn => Assert.Equal("", turn.SnapshotId));
        Assert.Contains(session.Chat.Timeline.Items, entry => entry.Title == "Snapshot event" && entry.SnapshotId == "");
    }

    [Fact]
    public async Task DeletingSnapshotMessagesKeepsTimelineEntriesByDefault()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = await MakeSnapshotEligibleAsync(session);
        var draft = await session.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        await session.Chat.Transcript.CommitSnapshotDraftAsync(draft!);
        var snapshot = session.Chat.Transcript.SnapshotFor(activeTurnId)!;
        var coveredTurnIds = draft!.CoveredTurnIds.ToHashSet(StringComparer.Ordinal);

        await session.Chat.Transcript.DeleteSnapshotAsync(snapshot.Id, SnapshotDeleteMethod.DeleteCoveredMessages, false);

        Assert.Null(session.Chat.Transcript.SnapshotFor(activeTurnId));
        Assert.DoesNotContain(session.Chat.Transcript.Items, turn => coveredTurnIds.Contains(turn.Id));
        Assert.Contains(session.Chat.Timeline.Items, entry => entry.Title == "Snapshot event" && entry.SnapshotId == "");
    }

    [Fact]
    public async Task CharacterTraitLibraryPersistsAcrossSessions()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var sessionA = NewSession(liveStore, generator);
        var sessionB = NewSession(liveStore, generator);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        sessionA.Chat.CharacterTraitLibrary.State.CoreDrives =
        [
            new("custom-drive", "Custom Drive", "Custom hover.")
        ];
        await sessionA.Chat.CharacterTraitLibrary.MarkChangedAsync();

        Assert.Equal("custom-drive", sessionB.Chat.CharacterTraitLibrary.State.CoreDrives.Single().Id);
    }

    [Fact]
    public async Task NarratorProfileHasDefaultsAndPersistsAcrossSessions()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var sessionA = NewSession(liveStore, generator);
        var sessionB = NewSession(liveStore, generator);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        Assert.Equal("cinematic-descriptive", sessionA.Chat.NarratorProfile.State.VoicePreset);
        Assert.Equal(1, sessionA.Chat.NarratorProfile.State.SetupDepth);

        sessionA.Chat.NarratorProfile.State.VoicePreset = "noir-observer";
        sessionA.Chat.NarratorProfile.State.Foreshadowing = 2;
        sessionA.Chat.NarratorProfile.State.CustomGuidance = "Keep the narration dry and observant.";
        await sessionA.Chat.NarratorProfile.MarkChangedAsync();

        Assert.Equal("noir-observer", sessionB.Chat.NarratorProfile.State.VoicePreset);
        Assert.Equal(2, sessionB.Chat.NarratorProfile.State.Foreshadowing);
        Assert.Equal("Keep the narration dry and observant.", sessionB.Chat.NarratorProfile.State.CustomGuidance);
    }

    [Fact]
    public async Task NewStoryCanCopyNarratorProfileFromCurrentStory()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var voiceKey = ModelSelectionKey.Build("provider", "voice-model");
        session.Chat.NarratorProfile.State.VoicePreset = "mythic-fable";
        session.Chat.NarratorProfile.State.DirectionStrength = 2;
        session.Chat.NarratorProfile.State.VoiceSelections[voiceKey] = Voice("copied-narrator", "Copied narrator");
        await session.Chat.NarratorProfile.MarkChangedAsync();

        await session.Chats.AddAsync(new() { CopyNarratorProfile = true });

        Assert.Equal("mythic-fable", session.Chat.NarratorProfile.State.VoicePreset);
        Assert.Equal(2, session.Chat.NarratorProfile.State.DirectionStrength);
        Assert.Equal("copied-narrator", session.Chat.NarratorProfile.State.VoiceSelections[voiceKey].VoiceId);
        Assert.False(session.Chat.Transcript.Options.AutoSpeakNewMessages);
    }

    [Fact]
    public async Task NewStoryTtsSetupCanReplaceCopiedNarratorVoiceAndAutoSpeak()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var voiceKey = ModelSelectionKey.Build("provider", "voice-model");
        session.Chat.NarratorProfile.State.VoiceSelections[voiceKey] = Voice("copied-narrator", "Copied narrator");
        await session.Chat.NarratorProfile.MarkChangedAsync();

        await session.Chats.AddAsync(new()
        {
            CopyNarratorProfile = true,
            EnableTts = true,
            AutoSpeakNewMessages = true,
            NarratorVoiceSelections = new(StringComparer.Ordinal)
            {
                [voiceKey] = Voice("replacement-narrator", "Replacement narrator")
            }
        });

        Assert.Equal("replacement-narrator", session.Chat.NarratorProfile.State.VoiceSelections[voiceKey].VoiceId);
        Assert.True(session.Chat.Transcript.Options.AutoSpeakNewMessages);
    }

    [Fact]
    public async Task PromptContextUsesLatestSnapshotAsTranscriptBoundary()
    {
        var persistence = new SeedRoleplayPersistence();
        var document = await persistence.LoadChatDocumentAsync("ch1");
        var builder = new TranscriptPromptContextBuilder();

        var context = builder.BuildTurnContext(document, "turn-3", "", "Brief", null);

        Assert.Contains("Gemma:", context.TranscriptText, StringComparison.Ordinal);
        Assert.DoesNotContain("Narrator:", context.TranscriptText, StringComparison.Ordinal);
        Assert.DoesNotContain("Bella:", context.TranscriptText, StringComparison.Ordinal);
        Assert.Contains("Bella has entered the apartment", context.SnapshotText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SceneEditsBecomeWorkingStateForNextTurn()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();
        var previousTurn = session.Chat.Transcript.Items.Last();
        var previousLocationId = previousTurn.Scene.LocationId;
        var targetLocation = session.Chat.Locations.Items.First(location => location.Id != previousLocationId);

        await session.Chat.Locations.SetActiveAsync(targetLocation.Id);

        Assert.True(session.Chat.Transcript.State.WorkingScene.IsActive);
        Assert.Equal(previousLocationId, previousTurn.Scene.LocationId);
        Assert.Equal(targetLocation.Id, session.Chat.Locations.Active?.Id);

        await session.Chat.Transcript.PostManualAsync("Now we are here.", null);

        var newTurn = session.Chat.Transcript.Items.Last();
        Assert.Equal(targetLocation.Id, newTurn.Scene.LocationId);
        Assert.Equal(previousLocationId, previousTurn.Scene.LocationId);
        Assert.False(session.Chat.Transcript.State.WorkingScene.IsActive);
    }

    [Fact]
    public void PromptTranscriptIncludesInlineSceneTransitions()
    {
        var document = CreateTransitionDocument();
        var builder = new TranscriptPromptContextBuilder();

        var context = builder.BuildTurnContext(document, "turn-2", "", "Brief", document.Characters[1]);

        Assert.Contains("Library (previously Apartment).", context.TranscriptText, StringComparison.Ordinal);
        Assert.Contains("Gemma and Mara are present in the scene.", context.TranscriptText, StringComparison.Ordinal);
        Assert.Contains("Gemma: Second message.", context.TranscriptText, StringComparison.Ordinal);
        Assert.DoesNotContain("Scene changes", context.TranscriptText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SceneTransitionEntryRendersClickableEntityMentions()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();
        var mention = new SceneTransitionMention(EntityTypes.Character, session.Chat.Characters.Items[0].Id, session.Chat.Characters.Items[0].Name);
        var delta = new SceneTransitionDelta(false, [new(SceneTransitionLineKind.CharactersLeft, [mention])]);
        (string Type, string? Id) opened = default;

        var component = context.Render<SceneTransitionEntry>(parameters => parameters
            .AddCascadingValue(session)
            .Add(component => component.Delta, delta)
            .Add(component => component.OnOpenEntities, value => opened = value));

        Assert.Contains("left the scene", component.Markup, StringComparison.Ordinal);
        component.Find(".entity-mention").Click();

        Assert.Equal("characters", opened.Type);
        Assert.Equal(mention.Id, opened.Id);
    }

    [Fact]
    public async Task SceneTransitionEntrySeparatesMentionsAndSuffixText()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();
        var gemma = new SceneTransitionMention(EntityTypes.Character, "c2", "Gemma");
        var lucia = new SceneTransitionMention(EntityTypes.Character, "c1", "Lucia");
        var delta = new SceneTransitionDelta(false, [new(SceneTransitionLineKind.CharactersPresent, [gemma, lucia])]);

        var component = context.Render<SceneTransitionEntry>(parameters => parameters
            .AddCascadingValue(session)
            .Add(component => component.Delta, delta));

        Assert.Equal("and", component.Find(".scene-transition-separator").TextContent.Trim());
        Assert.Equal("are present in the scene.", component.Find(".scene-transition-suffix").TextContent.Trim());
    }

    [Fact]
    public async Task PromptContextIncludesCharacterPronouns()
    {
        var persistence = new SeedRoleplayPersistence();
        var document = await persistence.LoadChatDocumentAsync("ch1");
        document.Characters[0].Pronouns = ["she/her"];
        document.Characters[1].Pronouns = ["they/them"];
        var builder = new TranscriptPromptContextBuilder();

        var context = builder.BuildTurnContext(document, "turn-3", "", "Brief", document.Characters[0]);
        var snapshot = builder.BuildSnapshotContext(document, "turn-3");

        Assert.Contains("- Pronouns: she/her", context.ActorText, StringComparison.Ordinal);
        Assert.Contains("- Pronouns: she/her", context.CharactersInSceneText, StringComparison.Ordinal);
        Assert.Contains("Pronouns:", context.SelectionEligibleResponders, StringComparison.Ordinal);
        Assert.Contains("Bella (id: c1; she/her)", snapshot.Characters, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotContextIncludesReferencedDetailsAndPrivateIntent()
    {
        var document = CreateTransitionDocument();
        var builder = new TranscriptPromptContextBuilder();

        var snapshot = builder.BuildSnapshotContext(document, "turn-2");

        Assert.Equal("Library (id: l2)", snapshot.CurrentLocation);
        Assert.Contains("Apartment (id: l1)", snapshot.Locations, StringComparison.Ordinal);
        Assert.Contains("Library (id: l2)", snapshot.Locations, StringComparison.Ordinal);
        Assert.Contains("Lantern (id: i1)", snapshot.Items, StringComparison.Ordinal);
        Assert.Contains("Map (id: i2)", snapshot.Items, StringComparison.Ordinal);
        Assert.Contains("- Personality: Sharp and careful.", snapshot.CharacterDetails, StringComparison.Ordinal);
        Assert.Contains("- Traits: guarded, observant", snapshot.CharacterDetails, StringComparison.Ordinal);
        Assert.Contains("- Core drive: protect-the-map", snapshot.CharacterDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("Nadia", snapshot.CharacterDetails, StringComparison.Ordinal);
        Assert.Contains("**Apartment** (id: l1)", snapshot.LocationDetails, StringComparison.Ordinal);
        Assert.Contains("- Atmosphere: Tense and quiet.", snapshot.LocationDetails, StringComparison.Ordinal);
        Assert.Contains("**Library** (id: l2)", snapshot.LocationDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("Rooftop", snapshot.LocationDetails, StringComparison.Ordinal);
        Assert.Contains("[Gemma's private intent: Hide the map before Mara asks questions.]", snapshot.Messages, StringComparison.Ordinal);
        Assert.Contains("[Gemma's private intent: Hide the map before Mara asks questions.]", snapshot.TranscriptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TranscriptOptionsDefaultToAudioTagsHiddenAndBlocksHidden()
    {
        var persistence = new SeedRoleplayPersistence();
        var document = await persistence.LoadChatDocumentAsync("ch1");

        Assert.True(document.Transcript.Options.InjectAudioTags);
        Assert.True(document.Transcript.Options.HideAudioTags);
        Assert.False(document.Transcript.Options.ShowAppearanceBlocks);
        Assert.False(document.Transcript.Options.ShowProcessTraces);
        Assert.False(document.Transcript.Options.AutoSpeakNewMessages);
        Assert.False(document.Transcript.Options.SpeakActionsInNarratorVoice);
        Assert.Equal("Auto", document.Transcript.Options.TurnShape);
        Assert.False(document.Transcript.Options.TurnShapeLocked);
    }

    [Fact]
    public async Task TranscriptOptionsPersistAndPropagateAcrossSessions()
    {
        var persistence = new SeedRoleplayPersistence();
        await using (var liveStore = new LiveRoleplayStore(persistence, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1)))
        {
            var sessionA = NewSession(liveStore, new FakeTextGenerationService());
            var sessionB = NewSession(liveStore, new FakeTextGenerationService());
            await sessionA.InitializeAsync();
            await sessionB.InitializeAsync();

            await sessionA.Chat.Transcript.SetInjectAudioTagsAsync(true);
            await sessionA.Chat.Transcript.SetHideAudioTagsAsync(true);
            await sessionA.Chat.Transcript.SetShowAppearanceBlocksAsync(true);
            await sessionA.Chat.Transcript.SetShowProcessTracesAsync(true);
            await sessionA.Chat.Transcript.SetTurnShapeAsync("Extended");
            await sessionA.Chat.Transcript.SetTurnShapeLockedAsync(true);

            Assert.True(sessionB.Chat.Transcript.Options.InjectAudioTags);
            Assert.True(sessionB.Chat.Transcript.Options.HideAudioTags);
            Assert.True(sessionB.Chat.Transcript.Options.ShowAppearanceBlocks);
            Assert.True(sessionB.Chat.Transcript.Options.ShowProcessTraces);
            Assert.Equal("Extended", sessionB.Chat.Transcript.Options.TurnShape);
            Assert.True(sessionB.Chat.Transcript.Options.TurnShapeLocked);
        }

        await using var reloadedStore = new LiveRoleplayStore(persistence, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var reloadedSession = NewSession(reloadedStore, new FakeTextGenerationService());
        await reloadedSession.InitializeAsync();

        Assert.True(reloadedSession.Chat.Transcript.Options.InjectAudioTags);
        Assert.True(reloadedSession.Chat.Transcript.Options.HideAudioTags);
        Assert.True(reloadedSession.Chat.Transcript.Options.ShowAppearanceBlocks);
        Assert.True(reloadedSession.Chat.Transcript.Options.ShowProcessTraces);
        Assert.Equal("Extended", reloadedSession.Chat.Transcript.Options.TurnShape);
        Assert.True(reloadedSession.Chat.Transcript.Options.TurnShapeLocked);
    }

    static void ConfigureSnapshotComponentContext(BunitContext context)
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<DialogHelper>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
    }

    static async Task<string> MakeSnapshotEligibleAsync(RoleplaySession session)
    {
        var target = await MakeSnapshotEligibleTargetAsync(session);
        return target.TargetTurnId;
    }

    static async Task<SnapshotDraftTarget> MakeSnapshotEligibleTargetAsync(RoleplaySession session)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var lastTurnId = session.Chat.Transcript.Items.Last().Id;
            var target = session.Chat.Transcript.GetSnapshotDraftTarget(lastTurnId);
            if (target?.CanCreate == true)
                return target;

            await session.Chat.Transcript.PostManualAsync($"Snapshot setup message {session.Chat.Transcript.Items.Count + 1}.", null);
        }

        throw new InvalidOperationException("Snapshot setup failed to create an eligible transcript range.");
    }

    static LiveRoleplayStore NewLiveStore(TimeSpan? ttl = null) =>
        new(new SeedRoleplayPersistence(), ttl ?? TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    static RpChatDocument CreateTransitionDocument()
    {
        var firstScene = new RpSceneFrame
        {
            LocationId = "l1",
            LocationName = "Apartment",
            InSceneCharacterIds = ["c1", "c2"],
            InSceneItemIds = ["i1"]
        };
        var secondScene = new RpSceneFrame
        {
            LocationId = "l2",
            LocationName = "Library",
            InSceneCharacterIds = ["c2", "c3"],
            InSceneItemIds = ["i2"]
        };
        var now = DateTime.UtcNow;
        return new()
        {
            Chat = new() { Id = "ch-test", Title = "Transition Test" },
            Locations =
            [
                new() { Id = "l1", Name = "Apartment", Summary = "A private apartment.", Description = "Dining table and narrow hallway.", Atmosphere = "Tense and quiet.", Features = "Kitchen island, balcony door" },
                new() { Id = "l2", Name = "Library", Summary = "A quiet map archive.", Description = "Tall shelves and study lamps.", Atmosphere = "Dusty and focused.", Features = "Map cases, rolling ladders" },
                new() { Id = "l3", Name = "Rooftop", Summary = "Unrelated roof garden." }
            ],
            Characters =
            [
                new() { Id = "c1", Name = "Lucia" },
                new()
                {
                    Id = "c2",
                    Name = "Gemma",
                    Summary = "Keeps careful watch over the archive.",
                    Voice = "Dry and precise.",
                    Personality = "Sharp and careful.",
                    CoreDrive = "protect-the-map",
                    CoreFear = "losing-control",
                    HiddenTruth = "already knows the route",
                    Traits = ["guarded", "observant"],
                    Limits = ["No sudden confession"]
                },
                new() { Id = "c3", Name = "Mara" },
                new() { Id = "c4", Name = "Nadia", Personality = "Unrelated witness." }
            ],
            Items =
            [
                new() { Id = "i1", Name = "Lantern" },
                new() { Id = "i2", Name = "Map" }
            ],
            Transcript = new()
            {
                RootScene = CloneScene(firstScene),
                ActiveLeafTurnId = "turn-2",
                Turns =
                [
                    new()
                    {
                        Id = "turn-1",
                        CreatedUtc = now,
                        UpdatedUtc = now,
                        AuthorName = "Lucia",
                        ActorName = "Lucia",
                        Body = "First message.",
                        Scene = CloneScene(firstScene)
                    },
                    new()
                    {
                        Id = "turn-2",
                        ParentTurnId = "turn-1",
                        CreatedUtc = now.AddMinutes(1),
                        UpdatedUtc = now.AddMinutes(1),
                        AuthorCharacterId = "c2",
                        AuthorName = "Gemma",
                        ActorCharacterId = "c2",
                        ActorName = "Gemma",
                        Body = "Second message.",
                        PrivateIntentByCharacterId = new() { ["c2"] = "Hide the map before Mara asks questions." },
                        Scene = CloneScene(secondScene)
                    }
                ]
            }
        };
    }

    static RpSceneFrame CloneScene(RpSceneFrame scene) => new()
    {
        LocationId = scene.LocationId,
        LocationName = scene.LocationName,
        InSceneCharacterIds = [.. scene.InSceneCharacterIds],
        InSceneItemIds = [.. scene.InSceneItemIds],
        Data = scene.Data.DeepClone().AsObject()
    };

    static CharacterVoiceSelection Voice(string id, string name) => new()
    {
        VoiceId = id,
        VoiceName = name,
        UpdatedUtc = DateTime.UtcNow
    };

    static RoleplaySession NewSession(LiveRoleplayStore liveStore, ITextGenerationService generator) =>
        new(liveStore, new FakeCapabilityCatalog(), generator);

    sealed class FakeCapabilityCatalog : IModelCapabilityCatalog
    {
        public string UserCatalogPath => "";

        public ModelGenerationCapabilities Resolve(AiProvider provider, AiProviderModel model) => model.Capabilities;

        public ModelGenerationCapabilities Resolve(string providerType, string modelId) => ModelGenerationCapabilities.Fallback;

        public void ApplyResolvedCapabilities(AiProvider provider)
        {
        }

        public void SaveUserCapabilities(string providerType, string modelId, ModelGenerationCapabilities capabilities)
        {
        }

        public void UpdateLiveGrokCapabilities(JsonNode languageModelsJson)
        {
        }
    }

    sealed class FakeTextGenerationService : ITextGenerationService
    {
        public int CyoaDecisionCalls { get; private set; }
        public int AutonomousCyoaTurnCalls { get; private set; }

        public Task<GeneratedTurnResult> GenerateTurnAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            GenerateTurnRequest request,
            TranscriptGenerationProgress? progress = null,
            CancellationToken cancellationToken = default)
        {
            var actor = document.Characters.FirstOrDefault(character => character.Id == request.RequestedActorCharacterId)
                ?? document.Characters.First();
            var now = DateTime.UtcNow;
            var trace = new RpGenerationTrace
            {
                Summary = $"Completed · {actor.Name} · Appearance -> Selection -> Planning -> Prose",
                Status = "completed",
                StartedUtc = now,
                CompletedUtc = now.AddSeconds(1),
                DurationSeconds = 1,
                ProviderId = "fake",
                ProviderName = "Fake",
                ModelId = "fake-model",
                Steps =
                [
                    new()
                    {
                        Id = "planning",
                        Label = "Planning",
                        Status = "completed",
                        StartedUtc = now,
                        CompletedUtc = now.AddSeconds(1),
                        DurationSeconds = 1,
                        RawOutput = "{}"
                    }
                ]
            };

            return Task.FromResult(new GeneratedTurnResult(
                actor.Id,
                actor.Name,
                new RpTurnPlan
                {
                    TurnShape = request.RequestedTurnShape,
                    Beat = "Test beat",
                    Intent = "Test intent",
                    ImmediateGoal = "Test goal",
                    WhyNow = "Test why now",
                    ChangeIntroduced = "Test change",
                    Guardrails = "Test guardrails"
                },
                new Dictionary<string, string> { [actor.Id] = $"{actor.Name} test appearance" },
                new Dictionary<string, string> { [actor.Id] = $"{actor.Name} test private intent" },
                CloneScene(document.Transcript.Turns.FirstOrDefault(turn => turn.Id == document.Transcript.ActiveLeafTurnId)?.Scene ?? document.Transcript.RootScene),
                $"Generated for {actor.Name}: {request.Guidance}".Trim(),
                trace));
        }

        public Task<GeneratedTurnResult> GeneratePlanAndProseAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            GeneratePlanAndProseRequest request,
            TranscriptGenerationProgress? progress = null,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var actorName = request.RequestedNarrator ? "Narrator" : request.ActorName;
            return Task.FromResult(new GeneratedTurnResult(
                request.ActorCharacterId,
                actorName,
                new RpTurnPlan
                {
                    TurnShape = request.RequestedTurnShape,
                    Beat = "Test replanned beat",
                    Intent = "Test replanned intent",
                    ImmediateGoal = "Test replanned goal",
                    WhyNow = "Test replanned why now",
                    ChangeIntroduced = "Test replanned change",
                    Guardrails = "Test replanned guardrails"
                },
                request.AppearanceByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                string.IsNullOrWhiteSpace(request.ActorCharacterId)
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal) { [request.ActorCharacterId] = $"{actorName} test replanned private intent" },
                CloneScene(request.Scene),
                $"Replanned for {actorName}: {request.Guidance}".Trim(),
                new RpGenerationTrace
                {
                    Summary = $"Completed Â· {actorName} Â· Planning -> Prose",
                    Status = "completed",
                    StartedUtc = now,
                    CompletedUtc = now.AddSeconds(1),
                    DurationSeconds = 1,
                    ProviderId = "fake",
                    ProviderName = "Fake",
                    ModelId = "fake-model",
                    Steps =
                    [
                        new()
                        {
                            Id = "planning",
                            Label = "Planning",
                            Status = "completed",
                            StartedUtc = now,
                            CompletedUtc = now.AddSeconds(1),
                            DurationSeconds = 1,
                            RawOutput = "{}"
                        },
                        new()
                        {
                            Id = "prose",
                            Label = "Prose",
                            Status = "completed",
                            StartedUtc = now,
                            CompletedUtc = now.AddSeconds(1),
                            DurationSeconds = 1,
                            RawOutput = "{}"
                        }
                    ]
                }));
        }

        public Task<GeneratedTurnResult> GenerateProseFromPlanAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            GenerateProseFromPlanRequest request,
            TranscriptGenerationProgress? progress = null,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var actorName = request.RequestedNarrator ? "Narrator" : request.ActorName;
            return Task.FromResult(new GeneratedTurnResult(
                request.ActorCharacterId,
                actorName,
                ClonePlan(request.Plan),
                request.AppearanceByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                request.PrivateIntentByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                CloneScene(request.Scene),
                $"Regenerated for {actorName}: {request.Guidance}".Trim(),
                new RpGenerationTrace
                {
                    Summary = $"Completed · {actorName} · Prose",
                    Status = "completed",
                    StartedUtc = now,
                    CompletedUtc = now.AddSeconds(1),
                    DurationSeconds = 1,
                    ProviderId = "fake",
                    ProviderName = "Fake",
                    ModelId = "fake-model",
                    Steps =
                    [
                        new()
                        {
                            Id = "prose",
                            Label = "Prose",
                            Status = "completed",
                            StartedUtc = now,
                            CompletedUtc = now.AddSeconds(1),
                            DurationSeconds = 1,
                            RawOutput = "{}"
                        }
                    ]
                }));
        }

        public Task<CyoaActorSelection> SelectCyoaActorAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            SelectCyoaActorRequest request,
            CancellationToken cancellationToken = default)
        {
            var character = document.Characters.First(character =>
                request.ControlledCharacterIds.Contains(character.Id, StringComparer.Ordinal)
                || !request.ForceControlled);
            return Task.FromResult(new CyoaActorSelection(character.Id, character.Name, false));
        }

        public Task<GeneratedCyoaDecision> GenerateCyoaDecisionAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            GenerateCyoaDecisionRequest request,
            TranscriptGenerationProgress? progress = null,
            CancellationToken cancellationToken = default)
        {
            CyoaDecisionCalls++;
            var now = DateTime.UtcNow;
            var trace = new RpGenerationTrace
            {
                Summary = "Completed - Choices",
                Status = "completed",
                StartedUtc = now,
                CompletedUtc = now.AddSeconds(1),
                DurationSeconds = 1,
                Steps =
                [
                    new()
                    {
                        Id = "cyoa-options",
                        Label = "Choices",
                        Status = "completed",
                        StartedUtc = now,
                        CompletedUtc = now.AddSeconds(1),
                        DurationSeconds = 1
                    }
                ]
            };
            var decision = new RpCyoaPendingDecision
            {
                Id = $"cyoa-{CyoaDecisionCalls}",
                ParentTurnId = request.ParentTurnId,
                Mode = request.Mode,
                ActorCharacterId = request.ActorCharacterId,
                ActorName = request.ActorName,
                RequestedNarrator = request.RequestedNarrator,
                CreatedUtc = now,
                Trace = trace,
                Options =
                [
                    new()
                    {
                        Id = "option-continue",
                        Direction = RpCyoaDirections.Continue,
                        Title = "Continue",
                        Summary = "Continue the current beat.",
                        Guidance = "Continue the current beat.",
                        ActorCharacterId = request.ActorCharacterId,
                        ActorName = request.ActorName,
                        RequestedNarrator = request.RequestedNarrator,
                        Plan = new() { TurnShape = "Brief", Beat = "Continue beat" }
                    }
                ]
            };
            return Task.FromResult(new GeneratedCyoaDecision(decision, trace));
        }

        public Task<GeneratedTurnResult> GenerateSelectedCyoaTurnAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            GenerateSelectedCyoaTurnRequest request,
            TranscriptGenerationProgress? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GeneratedTurnResult> GenerateAutonomousCyoaTurnAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            GenerateAutonomousCyoaTurnRequest request,
            TranscriptGenerationProgress? progress = null,
            CancellationToken cancellationToken = default)
        {
            AutonomousCyoaTurnCalls++;
            return GenerateTurnAsync(
                document,
                providers,
                modelSelections,
                new(
                    request.ParentTurnId,
                    request.Mode,
                    "",
                    TurnShapeRules.BriefLabel,
                    request.ActorCharacterId,
                    request.ActorName,
                    request.RequestedNarrator),
                progress,
                cancellationToken);
        }

        public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            ActiveModelSelectionsState modelSelections,
            GenerateSnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return Task.FromResult(new GeneratedSnapshotResult(
                $"Snapshot for {request.TurnId}",
                [
                    new()
                    {
                        TurnNumber = 3,
                        Title = "Snapshot event",
                        Description = "Snapshot event details",
                        CharacterIds = ["c2"],
                        LocationIds = ["l1"],
                        ItemNames = ["Tesla Model S Plaid"]
                    }
                ],
                new RpGenerationTrace
                {
                    Summary = "Completed · Snapshot",
                    Status = "completed",
                    StartedUtc = now,
                    CompletedUtc = now.AddSeconds(1),
                    DurationSeconds = 1,
                    ProviderId = "fake",
                    ProviderName = "Fake",
                    ModelId = "fake-model",
                    Steps =
                    [
                        new()
                        {
                            Id = "snapshot",
                            Label = "Snapshot",
                            Status = "completed",
                            StartedUtc = now,
                            CompletedUtc = now.AddSeconds(1),
                            DurationSeconds = 1,
                            SystemPrompt = "Snapshot system prompt.",
                            UserPrompt = "Snapshot user prompt.",
                            RawOutput = "Snapshot raw output.",
                            StructuredOutputJson = "{\"summary\":\"Snapshot structured output.\"}"
                        }
                    ]
                },
                RelationshipUpdates:
                [
                    new()
                    {
                        RelationshipId = "relationship-c1-c2",
                        SourceCharacterId = "c1",
                        TargetCharacterId = "c2",
                        RelationshipTypes = ["Close Friend"],
                        PrivateTensions = ["Charged"],
                        HowSourceSeesTarget = "Bella sees Gemma as newly complicated but still trusted.",
                        HowTargetSeesSource = "Gemma sees Bella as closer after the pressure lands.",
                        PublicDynamic = "Best friends with sharper tension.",
                        Reason = "The snapshot range changes their emotional stance.",
                        EvidenceTurnNumbers = [2, 3]
                    }
                ]));
        }

        static RpSceneFrame CloneScene(RpSceneFrame source) => new()
        {
            LocationId = source.LocationId,
            LocationName = source.LocationName,
            InSceneCharacterIds = [.. source.InSceneCharacterIds],
            InSceneItemIds = [.. source.InSceneItemIds],
            Data = source.Data.DeepClone().AsObject()
        };

        static RpTurnPlan ClonePlan(RpTurnPlan source) => new()
        {
            TurnShape = source.TurnShape,
            Beat = source.Beat,
            Intent = source.Intent,
            ImmediateGoal = source.ImmediateGoal,
            WhyNow = source.WhyNow,
            ChangeIntroduced = source.ChangeIntroduced,
            Guardrails = source.Guardrails,
            Data = source.Data.DeepClone().AsObject()
        };
    }
}
