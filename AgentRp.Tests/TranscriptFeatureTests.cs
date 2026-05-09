using AgentRp.Components.Chat;
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

        var activeTurnId = sessionA.Chat.Transcript.Items.Last().Id;
        var draft = await sessionA.Chat.Transcript.CreateSnapshotDraftAsync(activeTurnId);
        Assert.NotNull(draft);
        Assert.Null(sessionB.Chat.Transcript.SnapshotFor(activeTurnId));
        Assert.DoesNotContain(sessionB.Chat.Timeline.Items, entry => entry.Title == "Snapshot event");
        await sessionA.Chat.Transcript.CommitSnapshotDraftAsync(draft!);

        var snapshot = sessionB.Chat.Transcript.SnapshotFor(activeTurnId);
        Assert.NotNull(snapshot);
        Assert.Equal("Snapshot for turn-3", snapshot!.Summary);
        Assert.All(sessionB.Chat.Transcript.Items.Where(turn => draft!.CoveredTurnIds.Contains(turn.Id)), turn => Assert.Equal(snapshot.Id, turn.SnapshotId));
        Assert.Equal("Take Bella's affection while reminding Jake she noticed his silence.", snapshot.PrivateIntentByCharacterId["c2"]);
        var snapshotTimelineEntry = Assert.Single(sessionB.Chat.Timeline.Items, entry => entry.SnapshotId == snapshot.Id);
        Assert.Equal("Snapshot event", snapshotTimelineEntry.Title);
        Assert.Equal("Turn 3", snapshotTimelineEntry.Date);
    }

    [Fact]
    public async Task UnwrappingSnapshotKeepsMessagesAndTimelineEntries()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var activeTurnId = session.Chat.Transcript.Items.Last().Id;
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

        var activeTurnId = session.Chat.Transcript.Items.Last().Id;
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

        session.Chat.NarratorProfile.State.VoicePreset = "mythic-fable";
        session.Chat.NarratorProfile.State.DirectionStrength = 2;
        await session.Chat.NarratorProfile.MarkChangedAsync();

        await session.Chats.AddAsync(new() { CopyNarratorProfile = true });

        Assert.Equal("mythic-fable", session.Chat.NarratorProfile.State.VoicePreset);
        Assert.Equal(2, session.Chat.NarratorProfile.State.DirectionStrength);
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
        Assert.Contains("-   Pronouns: she/her", context.CharactersInSceneText, StringComparison.Ordinal);
        Assert.Contains("Pronouns:", context.SelectionEligibleResponders, StringComparison.Ordinal);
        Assert.Contains("Bella (she/her)", snapshot.Characters, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TranscriptOptionsDefaultToAudioTagsVisibleAndBlocksHidden()
    {
        var persistence = new SeedRoleplayPersistence();
        var document = await persistence.LoadChatDocumentAsync("ch1");

        Assert.False(document.Transcript.Options.InjectAudioTags);
        Assert.False(document.Transcript.Options.HideAudioTags);
        Assert.False(document.Transcript.Options.ShowAppearanceBlocks);
        Assert.False(document.Transcript.Options.ShowProcessTraces);
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
            var trace = new RpTurnTrace
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
                new RpTurnTrace
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
                new RpTurnTrace
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
                        CharacterNames = ["Gemma"],
                        LocationNames = ["Devonshire Apartment 822"],
                        ItemNames = ["Tesla Model S Plaid"]
                    }
                ],
                new RpTurnTrace
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
                            RawOutput = "{}"
                        }
                    ]
                }));
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
