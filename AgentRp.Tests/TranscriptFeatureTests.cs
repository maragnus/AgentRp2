using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
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

            Assert.True(sessionB.Chat.Transcript.Options.InjectAudioTags);
            Assert.True(sessionB.Chat.Transcript.Options.HideAudioTags);
            Assert.True(sessionB.Chat.Transcript.Options.ShowAppearanceBlocks);
            Assert.True(sessionB.Chat.Transcript.Options.ShowProcessTraces);
        }

        await using var reloadedStore = new LiveRoleplayStore(persistence, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        var reloadedSession = NewSession(reloadedStore, new FakeTextGenerationService());
        await reloadedSession.InitializeAsync();

        Assert.True(reloadedSession.Chat.Transcript.Options.InjectAudioTags);
        Assert.True(reloadedSession.Chat.Transcript.Options.HideAudioTags);
        Assert.True(reloadedSession.Chat.Transcript.Options.ShowAppearanceBlocks);
        Assert.True(reloadedSession.Chat.Transcript.Options.ShowProcessTraces);
    }

    static LiveRoleplayStore NewLiveStore(TimeSpan? ttl = null) =>
        new(new SeedRoleplayPersistence(), ttl ?? TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

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

        public Task<GeneratedTurnResult> GenerateProseFromPlanAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
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
            GenerateSnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return Task.FromResult(new GeneratedSnapshotResult(
                $"Snapshot for {request.TurnId}",
                [
                    new()
                    {
                        WhenText = "Today",
                        Title = "Snapshot event",
                        Summary = "Snapshot event summary",
                        Details = "Snapshot event details",
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
