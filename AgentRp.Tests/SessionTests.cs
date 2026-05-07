using AgentRp.Components.Chat;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace AgentRp.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task TwoSessionsShareOneLoadedLiveChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);

        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.Equal(
            sessionA.Chat.Characters.Items.First(character => character.Id == "c1").InScene,
            sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene);
    }

    [Fact]
    public async Task SelectAsyncAwaitsFullChatContextBeforeSwitchCompletes()
    {
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore);
        await session.InitializeAsync();

        await session.Chats.SelectAsync("ch2");

        Assert.Equal("ch2", session.Chats.Active?.Id);
        Assert.NotEmpty(session.Chat.Characters.Items);
        Assert.NotEmpty(session.Chat.Locations.Items);
        Assert.NotEmpty(session.Chat.Images.Items);
        Assert.NotEmpty(session.Chat.Transcript.Items);
    }

    [Fact]
    public async Task CharacterChangeInOneSessionUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var notifications = 0;
        sessionB.Chat.Characters.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        var original = sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene;
        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.True(notifications > 0);
        Assert.NotEqual(original, sessionB.Chat.Characters.Items.First(character => character.Id == "c1").InScene);
    }

    [Fact]
    public async Task TranscriptPostInOneSessionUpdatesSecondSessionOnSameChat()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Transcript.PostManualAsync("Shared live transcript message.", null);

        Assert.Contains(sessionB.Chat.Transcript.Items, message => message.Body == "Shared live transcript message.");
    }

    [Fact]
    public async Task SessionOnDifferentChatIgnoresChatLocalNotification()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();
        await sessionB.Chats.SelectAsync("ch2");

        var notifications = 0;
        sessionB.Chat.Characters.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        await sessionA.Chat.Characters.ToggleInSceneAsync("c1");

        Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task ActiveChatSwitchUsesLiveMemoryInsteadOfFreshSeedClone()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Transcript.PostManualAsync("Memory should win.", null);
        await sessionB.Chats.SelectAsync("ch2");
        await sessionB.Chats.SelectAsync("ch1");

        Assert.Contains(sessionB.Chat.Transcript.Items, message => message.Body == "Memory should win.");
    }

    [Fact]
    public async Task ProviderChangesNotifyAllSessions()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        var notifications = 0;
        sessionB.Providers.Changed += () =>
        {
            notifications++;
            return Task.CompletedTask;
        };

        var provider = sessionA.Providers.Items.First();
        provider.Enabled = !provider.Enabled;
        await sessionA.Providers.MarkChangedAsync();

        Assert.True(notifications > 0);
        Assert.Equal(provider.Enabled, sessionB.Providers.Items.First(item => item.Id == provider.Id).Enabled);
    }

    [Fact]
    public async Task ProviderWidgetAutoLoadRunsOncePerProviderPerSession()
    {
        var widgetService = new CountingProviderWidgetService();
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await session.InitializeAsync();
        var providerId = session.Providers.Items.First().Id;

        await session.Providers.EnsureWidgetLoadedAsync(providerId);
        await session.Providers.EnsureWidgetLoadedAsync(providerId);

        Assert.Equal(1, widgetService.RefreshCalls);
        Assert.NotEmpty(session.Providers.Items.First(provider => provider.Id == providerId).Metrics);
    }

    [Fact]
    public async Task ProviderWidgetAutoLoadRunsAgainForNewSession()
    {
        var widgetService = new CountingProviderWidgetService();
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await sessionA.InitializeAsync();
        var providerId = sessionA.Providers.Items.First().Id;
        await sessionA.Providers.EnsureWidgetLoadedAsync(providerId);

        var sessionB = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await sessionB.InitializeAsync();
        await sessionB.Providers.EnsureWidgetLoadedAsync(providerId);

        Assert.Equal(2, widgetService.RefreshCalls);
    }

    [Fact]
    public async Task ProviderWidgetManualRefreshAlwaysReloads()
    {
        var widgetService = new CountingProviderWidgetService();
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(
            liveStore,
            new TestModelCapabilityCatalog(),
            providerWidgetService: widgetService);
        await session.InitializeAsync();
        var providerId = session.Providers.Items.First().Id;

        await session.Providers.RefreshWidgetAsync(providerId);
        await session.Providers.RefreshWidgetAsync(providerId);

        Assert.Equal(2, widgetService.RefreshCalls);
    }

    [Fact]
    public async Task UnreferencedInactiveChatsCanUnloadAfterTtl()
    {
        await using var liveStore = NewLiveStore(TimeSpan.FromMilliseconds(10));
        var session = NewSession(liveStore);
        await session.InitializeAsync();

        Assert.True(liveStore.IsChatLoaded("ch1"));

        await session.Chats.SelectAsync("ch2");
        await Task.Delay(20);
        liveStore.CleanupExpiredChats();

        Assert.False(liveStore.IsChatLoaded("ch1"));
        Assert.True(liveStore.IsChatLoaded("ch2"));
    }

    [Fact]
    public async Task ChatAreaRerendersFromCrossSessionTranscriptNotification()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        context.Services.AddSingleton<ITranscriptBodyRenderer, TranscriptBodyRenderer>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        await using var liveStore = NewLiveStore();
        var sessionA = NewSession(liveStore);
        var sessionB = NewSession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();
        var component = context.Render<ChatArea>(parameters => parameters.AddCascadingValue(sessionB));

        await sessionA.Chat.Transcript.PostManualAsync("Rendered from another session.", null);

        component.WaitForAssertion(() => Assert.Contains("Rendered from another session.", component.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChatAreaLocksFooterWhileTranscriptOperationRuns()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        context.Services.AddSingleton<ITranscriptBodyRenderer, TranscriptBodyRenderer>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var component = context.Render<ChatArea>(parameters => parameters.AddCascadingValue(session));

        var operation = session.Chat.Transcript.GenerateAsync("", null, false, "automatic", "Brief");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        component.WaitForAssertion(() =>
        {
            Assert.NotNull(component.Find(".claude-composer.is-locked"));
            Assert.NotNull(component.Find("textarea[disabled]"));
            Assert.Contains("Generating...", component.Markup, StringComparison.Ordinal);
            Assert.False(session.Chat.Transcript.Options.ShowProcessTraces);
            Assert.NotNull(component.Find(".process-row.is-live"));
            Assert.DoesNotContain("process-steps", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Appearance", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Selection", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Planning", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Prose", component.Markup, StringComparison.Ordinal);
            Assert.Contains("process-flow-step is-pending", component.Markup, StringComparison.Ordinal);
            Assert.Contains("fa-spinner", component.Markup, StringComparison.Ordinal);
            Assert.True(component.FindAll(".claude-composer-actions button[disabled]").Count > 0);
        });

        await component.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", CtrlKey = true });
        Assert.Equal(1, generation.GenerateCalls);

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindAll(".process-row.is-live"));
            Assert.Contains(BlockingTextGenerationService.GeneratedBody, component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task TranscriptStoreIgnoresOverlappingOperationsUntilCurrentOperationCompletes()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();

        var operation = session.Chat.Transcript.GenerateAsync("", null, false, "automatic", "Brief");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(session.Chat.Transcript.IsBusy);
        Assert.Equal("Generating...", session.Chat.Transcript.BusyMessage);

        await session.Chat.Transcript.PostManualAsync("This should not be posted while busy.", null);

        Assert.DoesNotContain(session.Chat.Transcript.Items, message => message.Body == "This should not be posted while busy.");
        Assert.Equal(1, generation.GenerateCalls);

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(session.Chat.Transcript.IsBusy);
        Assert.Equal("", session.Chat.Transcript.BusyMessage);
        Assert.Contains(session.Chat.Transcript.Items, message => message.Body == BlockingTextGenerationService.GeneratedBody);
    }

    [Fact]
    public void TurnShapePickerOptionsNormalizeKnownLabels()
    {
        Assert.Equal("Brief", TurnShapePickerOptions.Normalize("brief"));
        Assert.Equal("Brief", TurnShapePickerOptions.Normalize("Brief"));
        Assert.Equal("Silent Monologue", TurnShapePickerOptions.Normalize("silent-monologue"));
        Assert.Equal("Silent Monologue", TurnShapePickerOptions.Normalize("silent monologue"));
        Assert.Equal("Brief", TurnShapePickerOptions.Normalize(""));
        Assert.Equal("Auto", TurnShapePickerOptions.Normalize("Auto"));
        Assert.Equal("Brief", TurnShapePickerOptions.NormalizeExplicit("Auto"));
    }

    [Fact]
    public async Task RegenerationHidesOriginalTurnWhileRunning()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();

        var operation = session.Chat.Transcript.RegenerateAsync(original.Id, original.Guidance, null, "Extended");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(session.Chat.Transcript.IsBusy);
        Assert.Equal("Regenerating...", session.Chat.Transcript.BusyMessage);
        Assert.DoesNotContain(session.Chat.Transcript.Items, turn => turn.Id == original.Id);
        Assert.Equal(original.ParentTurnId, session.Chat.Transcript.Items.Last().Id);

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegenerationCommitsSiblingWithRequestedTurnShape()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();

        var operation = session.Chat.Transcript.RegenerateAsync(original.Id, original.Guidance, null, "Silent Monologue");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        var regenerated = session.Chat.Transcript.Items.Last();
        Assert.NotEqual(original.Id, regenerated.Id);
        Assert.Equal(original.ParentTurnId, regenerated.ParentTurnId);
        Assert.Equal("Silent Monologue", regenerated.Plan.TurnShape);
        Assert.Equal("Silent Monologue", generation.Requests.Single().RequestedTurnShape);
        Assert.Equal(2, session.Chat.Transcript.SiblingsFor(regenerated.Id).Count);
    }

    [Fact]
    public async Task FailedRegenerationCommitsFailedSiblingAndKeepsOriginalHidden()
    {
        var generation = new FailingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();
        var original = session.Chat.Transcript.Items.Last();

        await session.Chat.Transcript.RegenerateAsync(original.Id, original.Guidance, null, "Extended");

        var failedTurn = session.Chat.Transcript.Items.Last();
        Assert.NotEqual(original.Id, failedTurn.Id);
        Assert.Equal(original.ParentTurnId, failedTurn.ParentTurnId);
        Assert.Equal("Extended", failedTurn.Plan.TurnShape);
        Assert.Equal("failed", failedTurn.Trace?.Status);
        Assert.DoesNotContain(session.Chat.Transcript.Items, turn => turn.Id == original.Id);
        Assert.Equal(2, session.Chat.Transcript.SiblingsFor(failedTurn.Id).Count);
    }

    [Fact]
    public async Task TranscriptStoreClearsLiveTraceAndPersistsFailedTrace()
    {
        var generation = new FailingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = NewSession(liveStore, generation);
        await session.InitializeAsync();

        await session.Chat.Transcript.GenerateAsync("", null, true, "automatic", "Brief");

        Assert.Null(session.Chat.Transcript.ActiveTrace);
        var failedTurn = session.Chat.Transcript.Items.Last();
        Assert.Equal("failed", failedTurn.Trace?.Status);
        Assert.Equal("failed", failedTurn.Trace?.Steps.Single().Status);
        Assert.NotNull(session.Chat.Transcript.LastBackgroundError);
    }

    static LiveRoleplayStore NewLiveStore(TimeSpan? ttl = null) =>
        new(new SeedRoleplayPersistence(), ttl ?? TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    static RoleplaySession NewSession(LiveRoleplayStore liveStore, ITextGenerationService? generator = null) =>
        new(liveStore, new TestModelCapabilityCatalog(), generator);

    sealed class BlockingTextGenerationService : ITextGenerationService
    {
        public const string GeneratedBody = "Generated while lock is held.";
        int _generateCalls;

        public int GenerateCalls => _generateCalls;
        public List<GenerateTurnRequest> Requests { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _generateCalls);
            Requests.Add(request);
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating · Appearance · Appearance -> Selection -> Planning -> Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "appearance", Label = "Appearance", Status = "running", StartedUtc = startedUtc },
                    new() { Id = "selection", Label = "Selection", Status = "pending" },
                    new() { Id = "planning", Label = "Planning", Status = "pending" },
                    new() { Id = "prose", Label = "Prose", Status = "pending" }
                ]
            };
            if (progress is not null)
                await progress.ReportAsync(trace);

            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            trace.Status = "completed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Steps[0].Status = "completed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;

            return new(
                "",
                "Narrator",
                new() { TurnShape = request.RequestedTurnShape },
                [],
                [],
                CloneScene(document.Transcript.RootScene),
                GeneratedBody,
                trace);
        }

        public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateSnapshotRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        static RpSceneFrame CloneScene(RpSceneFrame scene) => new()
        {
            LocationId = scene.LocationId,
            LocationName = scene.LocationName,
            InSceneCharacterIds = [.. scene.InSceneCharacterIds],
            InSceneItemIds = [.. scene.InSceneItemIds]
        };
    }

    sealed class FailingTextGenerationService : ITextGenerationService
    {
        public async Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default)
        {
            var startedUtc = DateTime.UtcNow;
            var trace = new RpTurnTrace
            {
                Summary = "Generating · Prose",
                Status = "running",
                StartedUtc = startedUtc,
                Steps =
                [
                    new() { Id = "prose", Label = "Prose", Status = "running", StartedUtc = startedUtc }
                ]
            };
            if (progress is not null)
                await progress.ReportAsync(trace);

            trace.Status = "failed";
            trace.CompletedUtc = DateTime.UtcNow;
            trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
            trace.Data["error"] = "Prose failed for test.";
            trace.Steps[0].Status = "failed";
            trace.Steps[0].CompletedUtc = trace.CompletedUtc;
            trace.Steps[0].DurationSeconds = trace.DurationSeconds;
            trace.Steps[0].Error = "Prose failed for test.";
            throw new TranscriptGenerationException("Prose failed for test.", trace);
        }

        public Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateSnapshotRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    sealed class TestModelCapabilityCatalog : IModelCapabilityCatalog
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

    sealed class CountingProviderWidgetService : IAiProviderWidgetService
    {
        int _refreshCalls;

        public int RefreshCalls => _refreshCalls;

        public Task<IReadOnlyList<AiProviderMetric>> RefreshMetricsAsync(AiProvider provider, CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref _refreshCalls);
            return Task.FromResult<IReadOnlyList<AiProviderMetric>>(
            [
                new()
                {
                    Id = $"metric-{count}",
                    Kind = "test",
                    Label = "Test",
                    Value = count.ToString(),
                    RefreshedUtc = DateTime.UtcNow
                }
            ]);
        }

        public Task<IReadOnlyList<ManagedEndpointStatusView>> GetHuggingFaceStatusesAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ManagedEndpointStatusView>>([]);

        public Task<ManagedEndpointStatusView> ExecuteHuggingFaceActionAsync(AiProvider provider, AiProviderModel model, ManagedEndpointAction action, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
