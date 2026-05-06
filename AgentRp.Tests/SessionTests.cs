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
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);

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
        var session = new RoleplaySession(liveStore);
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
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
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
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
        await sessionA.InitializeAsync();
        await sessionB.InitializeAsync();

        await sessionA.Chat.Transcript.PostManualAsync("Shared live transcript message.", null);

        Assert.Contains(sessionB.Chat.Transcript.Items, message => message.Body == "Shared live transcript message.");
    }

    [Fact]
    public async Task SessionOnDifferentChatIgnoresChatLocalNotification()
    {
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
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
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
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
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
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
    public async Task UnreferencedInactiveChatsCanUnloadAfterTtl()
    {
        await using var liveStore = NewLiveStore(TimeSpan.FromMilliseconds(10));
        var session = new RoleplaySession(liveStore);
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
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        await using var liveStore = NewLiveStore();
        var sessionA = new RoleplaySession(liveStore);
        var sessionB = new RoleplaySession(liveStore);
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
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(liveStore, generation);
        await session.InitializeAsync();
        var component = context.Render<ChatArea>(parameters => parameters.AddCascadingValue(session));

        var operation = session.Chat.Transcript.GenerateAsync("", null, false, "automatic", "Brief");
        await generation.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        component.WaitForAssertion(() =>
        {
            Assert.NotNull(component.Find(".claude-composer.is-locked"));
            Assert.NotNull(component.Find("textarea[disabled]"));
            Assert.Contains("Generating...", component.Markup, StringComparison.Ordinal);
            Assert.True(component.FindAll(".claude-composer-actions button[disabled]").Count > 0);
        });

        await component.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", CtrlKey = true });
        Assert.Equal(1, generation.GenerateCalls);

        generation.Release.SetResult();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TranscriptStoreIgnoresOverlappingOperationsUntilCurrentOperationCompletes()
    {
        var generation = new BlockingTextGenerationService();
        await using var liveStore = NewLiveStore();
        var session = new RoleplaySession(liveStore, generation);
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

    static LiveRoleplayStore NewLiveStore(TimeSpan? ttl = null) =>
        new(new SeedRoleplayPersistence(), ttl ?? TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    sealed class BlockingTextGenerationService : ITextGenerationService
    {
        public const string GeneratedBody = "Generated while lock is held.";
        int _generateCalls;

        public int GenerateCalls => _generateCalls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateTurnRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _generateCalls);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);

            return new(
                "",
                "Narrator",
                new() { TurnShape = request.RequestedTurnShape },
                [],
                [],
                CloneScene(document.Transcript.RootScene),
                GeneratedBody,
                new()
                {
                    Status = "completed",
                    StartedUtc = DateTime.UtcNow,
                    CompletedUtc = DateTime.UtcNow
                });
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
}
