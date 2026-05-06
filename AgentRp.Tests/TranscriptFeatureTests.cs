using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;

namespace AgentRp.Tests;

public sealed class TranscriptFeatureTests
{
    [Fact]
    public async Task EditTurnCreatesSiblingBranchAndSwitchesActivePath()
    {
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(liveStore, new FakeTextGenerationService());
        await session.InitializeAsync();

        var turnToEdit = session.Chat.Transcript.Items[1];
        await session.Chat.Transcript.EditTurnAsync(turnToEdit.Id, "Alternate Bella branch.");

        Assert.Equal("Alternate Bella branch.", session.Chat.Transcript.Items.Last().Body);
        Assert.Equal(2, session.Chat.Transcript.SiblingsFor(session.Chat.Transcript.Items.Last().Id).Count);
    }

    [Fact]
    public async Task SelectingSiblingRestoresOriginalBranchAcrossSessions()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var sessionA = new RoleplaySession(liveStore, generator);
        var sessionB = new RoleplaySession(liveStore, generator);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var originalTurn = sessionA.Chat.Transcript.Items[1];
        await sessionA.Chat.Transcript.EditTurnAsync(originalTurn.Id, "Alternate Bella branch.");

        await sessionB.Chat.Transcript.SelectSiblingAsync(originalTurn.Id);

        Assert.Equal("turn-3", sessionA.Chat.Transcript.Items.Last().Id);
        Assert.Equal("turn-3", sessionB.Chat.Transcript.Items.Last().Id);
    }

    [Fact]
    public async Task CreatingSnapshotUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var sessionA = new RoleplaySession(liveStore, generator);
        var sessionB = new RoleplaySession(liveStore, generator);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var activeTurnId = sessionA.Chat.Transcript.Items.Last().Id;
        await sessionA.Chat.Transcript.CreateSnapshotAsync(activeTurnId);

        var snapshot = sessionB.Chat.Transcript.SnapshotFor(activeTurnId);
        Assert.NotNull(snapshot);
        Assert.Equal("Snapshot for turn-3", snapshot!.Summary);
        var fact = Assert.Single(snapshot.Facts);
        Assert.Equal("Snapshot fact", fact.Title);
        var snapshotTimelineEntry = Assert.Single(snapshot.TimelineEntries);
        Assert.Equal("Snapshot event", snapshotTimelineEntry.Title);
        Assert.Contains(sessionB.Chat.Timeline.Items, entry => entry.Id == snapshotTimelineEntry.TimelineEntryId && entry.Title == "Snapshot event");
    }

    [Fact]
    public async Task CharacterTraitLibraryPersistsAcrossSessions()
    {
        await using var liveStore = NewLiveStore();
        var generator = new FakeTextGenerationService();
        var sessionA = new RoleplaySession(liveStore, generator);
        var sessionB = new RoleplaySession(liveStore, generator);
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

    static LiveRoleplayStore NewLiveStore(TimeSpan? ttl = null) =>
        new(new SeedRoleplayPersistence(), ttl ?? TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    sealed class FakeTextGenerationService : ITextGenerationService
    {
        public Task<GeneratedTurnResult> GenerateTurnAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            GenerateTurnRequest request,
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

        public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(
            RpChatDocument document,
            IReadOnlyList<AiProvider> providers,
            GenerateSnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            var turn = document.Transcript.Turns.First(item => item.Id == request.TurnId);
            var now = DateTime.UtcNow;
            return Task.FromResult(new GeneratedSnapshotResult(
                $"Snapshot for {request.TurnId}",
                "Earlier continuity",
                [
                    new()
                    {
                        Title = "Snapshot fact",
                        Summary = "Snapshot fact summary",
                        Details = "Snapshot fact details",
                        CharacterNames = ["Gemma"],
                        LocationNames = ["Devonshire Apartment 822"],
                        ItemNames = ["Tesla Model S Plaid"]
                    }
                ],
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
                turn.AppearanceByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                CloneScene(turn.Scene),
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
            InSceneItemIds = [.. source.InSceneItemIds]
        };
    }
}
