using System.Text.Json.Nodes;
using AgentRp.Components.Common;
using AgentRp.Components.Entities;
using AgentRp.Models;
using AgentRp.Components.Providers;
using AgentRp.Components.Shell;
using AgentRp.Services;
using AgentRp.Session;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace AgentRp.Tests;

public sealed class UiCompositionPolicyTests
{
    [Fact]
    public async Task SidebarRendersRealSessionDataWithoutFakeMetrics()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        context.Services.AddSingleton<IAiProviderWidgetService, TestProviderWidgetService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<Sidebar>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnOpenModal, _ => Task.CompletedTask)
            .Add(item => item.OnOpenEntities, _ => Task.CompletedTask));

        Assert.Contains(session.Chat.Items.Items[0].Name, component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Tok In:", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Top-P", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Primary User", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SidebarExposesStoryAssistantWhenStoryIsActive()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        context.Services.AddSingleton<IAiProviderWidgetService, TestProviderWidgetService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<Sidebar>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnOpenModal, _ => Task.CompletedTask)
            .Add(item => item.OnOpenEntities, _ => Task.CompletedTask));

        var storyIndex = component.Markup.IndexOf("Switch current story", StringComparison.Ordinal);
        var assistantIndex = component.Markup.IndexOf("Story Assistant", StringComparison.Ordinal);
        Assert.True(storyIndex >= 0);
        Assert.True(assistantIndex > storyIndex);
        Assert.NotNull(component.Find(".sidebar-assistant-entry"));
    }

    [Fact]
    public async Task SidebarLocationPickerFollowsActiveStoryAfterEmptyStory()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        context.Services.AddSingleton<IAiProviderWidgetService, TestProviderWidgetService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        await session.Chats.AddAsync(StoryCreationOptions.Blank());

        var component = context.Render<Sidebar>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnOpenModal, _ => Task.CompletedTask)
            .Add(item => item.OnOpenEntities, _ => Task.CompletedTask));
        var overlays = context.Render<OverlayHost>();

        Assert.Contains("No location", component.Markup, StringComparison.Ordinal);

        await session.Chats.SelectAsync("ch1");

        Assert.NotEmpty(session.Chat.Locations.Items);
        Assert.Equal("Devonshire Apartment 822", session.Chat.Locations.Active?.Name);

        component.WaitForAssertion(() =>
            Assert.Contains("Devonshire Apartment 822", component.Find("[title='Switch current location']").TextContent, StringComparison.Ordinal));

        await component.Find("[title='Switch current location']").ClickAsync(new());

        overlays.WaitForAssertion(() =>
            Assert.Contains("Devonshire Apartment 822", overlays.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SidebarModelPickerGroupsEnabledChatModelsByProvider()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        context.Services.AddSingleton<IAiProviderWidgetService, TestProviderWidgetService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        session.Providers.Items[0].Models.Add(new()
        {
            Id = "text-capable-but-not-chat-selected",
            Enabled = true,
            Roles = [AiModelRole.Image],
            Capabilities = new() { TextInput = true, TextOutput = true, ImageOutput = true }
        });

        var component = context.Render<Sidebar>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnOpenModal, _ => Task.CompletedTask)
            .Add(item => item.OnOpenEntities, _ => Task.CompletedTask));
        var overlays = context.Render<OverlayHost>();

        component.Find(".sidebar-model-panel .sidebar-row").Click();

        Assert.Contains("Chat Models", overlays.Markup, StringComparison.Ordinal);
        Assert.Contains("Grok / xAI", overlays.Markup, StringComparison.Ordinal);
        Assert.Contains("OpenAI", overlays.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("text-capable-but-not-chat-selected", overlays.Markup, StringComparison.Ordinal);
        Assert.NotNull(overlays.Find(".overlay-popover.sidebar-model-popover"));
        Assert.NotNull(overlays.Find(".sidebar-model-option.is-selected"));
        Assert.Contains("fa-check", overlays.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SidebarModelPickerSelectionUpdatesGlobalActiveModel()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        context.Services.AddSingleton<IAiProviderWidgetService, TestProviderWidgetService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<Sidebar>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnOpenModal, _ => Task.CompletedTask)
            .Add(item => item.OnOpenEntities, _ => Task.CompletedTask));
        var overlays = context.Render<OverlayHost>();
        component.Find(".sidebar-model-panel .sidebar-row").Click();
        var option = overlays.FindAll(".sidebar-model-option")
            .First(button => button.TextContent.Contains("grok-4-0709", StringComparison.Ordinal));

        await option.ClickAsync(new());

        var active = session.ModelSelection.Resolve(AiModelRole.Chat);
        Assert.Equal("grok-4-0709", active?.Model.Id);
    }

    [Fact]
    public async Task ModelTuningRendersActiveModelWithoutFakeSidebarMetrics()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IJSRuntime, TestJsRuntime>();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(session.Providers.Items);
        var component = context.Render<ModelTuningModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnClose, () => Task.CompletedTask));

        Assert.NotNull(active);
        Assert.Contains(active!.Model.Id, component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Tok In:", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Top-P", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderModelListKeepsSelectedModelsAtTheTop()
    {
        using var context = new BunitContext();
        var models = new List<AiProviderModel>
        {
            new()
            {
                Id = "newer-unselected",
                CreatedUnix = 30,
                Capabilities = new() { TextInput = true, TextOutput = true }
            },
            new()
            {
                Id = "selected-image",
                CreatedUnix = 10,
                Enabled = true,
                Roles = [AiModelRole.Image],
                Capabilities = new() { TextInput = true, TextOutput = false, ImageOutput = true }
            },
            new()
            {
                Id = "selected-chat",
                CreatedUnix = 5,
                Enabled = true,
                Roles = [AiModelRole.Chat],
                Capabilities = new() { TextInput = true, TextOutput = true }
            }
        };

        var component = context.Render<ProviderModelList>(parameters => parameters.Add(item => item.Models, models));
        var rowTitles = component.FindAll(".provider-model-title strong").Select(item => item.TextContent.Trim()).ToList();

        Assert.Equal(["selected-image", "selected-chat", "newer-unselected"], rowTitles);
    }

    [Fact]
    public void ProviderModelListShowsSetupModelsOnlyAfterOptIn()
    {
        using var context = new BunitContext();
        var models = new List<AiProviderModel>
        {
            new()
            {
                Id = "ready",
                Capabilities = new() { TextInput = true, TextOutput = true }
            },
            new()
            {
                Id = "needs-setup",
                Capabilities = new() { TextInput = false, TextOutput = false, ImageOutput = false }
            }
        };

        var component = context.Render<ProviderModelList>(parameters => parameters.Add(item => item.Models, models));

        Assert.Contains("ready", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("needs-setup", component.Markup, StringComparison.Ordinal);

        component.Find("button[title='Show models needing setup']").Click();

        Assert.Contains("needs-setup", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderModelListShowsSetupModelsWhenAllModelsNeedSetup()
    {
        using var context = new BunitContext();
        var models = new List<AiProviderModel>
        {
            new()
            {
                Id = "needs-setup",
                Capabilities = new() { TextInput = false, TextOutput = false, ImageOutput = false }
            }
        };

        var component = context.Render<ProviderModelList>(parameters => parameters.Add(item => item.Models, models));

        Assert.Contains("needs-setup", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Show setup", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntityManagerShowsFixedNarratorRowWithoutCountingItAsCharacter()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IElevenLabsVoiceCatalogService, TestElevenLabsVoiceCatalogService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<EntityManagerModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.InitialType, "characters")
            .Add(item => item.OnSelectEntityImage, _ => Task.CompletedTask)
            .Add(item => item.OnClose, () => Task.CompletedTask));

        Assert.Contains("Narrator", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Story voice", component.Markup, StringComparison.Ordinal);
        Assert.Contains($">{session.Chat.Characters.Items.Count}<", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain($">{session.Chat.Characters.Items.Count + 1}<", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntityManagerDoesNotRenderStoryAssistantTab()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
        context.Services.AddScoped<IEntityNotifier, EntityNotifier>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IElevenLabsVoiceCatalogService, TestElevenLabsVoiceCatalogService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<EntityManagerModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.InitialType, "characters")
            .Add(item => item.OnSelectEntityImage, _ => Task.CompletedTask)
            .Add(item => item.OnClose, () => Task.CompletedTask));

        var tabs = component.FindAll(".entity-tab").Select(tab => tab.TextContent).ToList();
        Assert.DoesNotContain(tabs, tab => tab.Contains("Assistant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StoryAssistantShowsStarterWorkflowsOnlyWhenAssistantTranscriptIsEmpty()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<StoryAssistantPanel>(parameters => parameters.AddCascadingValue(session));

        Assert.Contains("Prepare a New Story", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Introduce Characters", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Introduce a Location", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Change the Scene", component.Markup, StringComparison.Ordinal);

        session.Chat.StoryAssistant.State.Items.Add(new()
        {
            Id = "assistant-message-1",
            Kind = StoryAssistantItemKind.UserMessage,
            Status = StoryAssistantItemStatus.Applied,
            Text = "Existing assistant transcript."
        });

        component.Dispose();
        var componentWithTranscript = context.Render<StoryAssistantPanel>(parameters => parameters.AddCascadingValue(session));

        Assert.DoesNotContain("Prepare a New Story", componentWithTranscript.Markup, StringComparison.Ordinal);
        Assert.Contains("Existing assistant transcript.", componentWithTranscript.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoryAssistantChatRowsShowLastMessageDateAndFinalDeleteAction()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        var messageDate = DateTime.UtcNow.AddDays(-1);
        session.Chat.StoryAssistant.State.Items.Add(new()
        {
            Id = "assistant-message-1",
            Kind = StoryAssistantItemKind.UserMessage,
            Status = StoryAssistantItemStatus.Applied,
            Text = "Existing assistant transcript.",
            CreatedUtc = messageDate,
            UpdatedUtc = messageDate
        });

        var component = context.Render<StoryAssistantModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnClose, () => Task.CompletedTask));

        Assert.Contains(RelativeDateFormatter.FormatDate(messageDate), component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("1 message", component.Markup, StringComparison.Ordinal);
        Assert.NotNull(component.Find("[aria-label='Delete chat']"));
    }

    [Fact]
    public async Task StoryAssistantTranscriptRerendersWhenStreamingMessageMutates()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        await using var store = NewLiveStore();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new RoleplaySession(
            store,
            storyAssistantService: new ScriptedStoryAssistantService(async callbacks =>
            {
                started.SetResult();
                await release.Task;
                await callbacks.AppendAssistantTextAsync("Live streamed response.", CancellationToken.None);
            }));
        await session.InitializeAsync();

        var component = context.Render<StoryAssistantPanel>(parameters => parameters.AddCascadingValue(session));
        var start = component.FindAll("button")
            .First(button => button.TextContent.Contains("Prepare a New Story", StringComparison.Ordinal));

        var clickTask = start.ClickAsync(new());
        await started.Task;

        component.WaitForAssertion(() => Assert.Contains("Loading...", component.Markup, StringComparison.Ordinal));
        release.SetResult();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Live streamed response.", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Loading...", component.Markup, StringComparison.Ordinal);
        });
        await clickTask;
    }

    static LiveRoleplayStore NewLiveStore() =>
        new(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    sealed class ScriptedStoryAssistantService(Func<IStoryAssistantCallbacks, Task> script) : IStoryAssistantService
    {
        public Task RunTurnAsync(
            RpChatDocument document,
            StoryAssistantChat assistantChat,
            IReadOnlyList<AiProvider> providers,
            GenerationRuntimeConfig runtimeConfig,
            StoryAssistantTurnRequest request,
            IStoryAssistantCallbacks callbacks,
            CancellationToken cancellationToken = default) =>
            script(callbacks);

        public Task ClearRemoteStateAsync(
            StoryAssistantChat assistantChat,
            IReadOnlyList<AiProvider> providers,
            GenerationRuntimeConfig runtimeConfig,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResolveWorkItemAsync(
            RpChatDocument document,
            StoryAssistantWorkItem workItem,
            StoryAssistantWorkItemResolution resolution,
            IStoryAssistantCallbacks callbacks,
            CancellationToken cancellationToken = default) =>
            new StoryEntityPatchService().ResolveWorkItemAsync(document, workItem, resolution, callbacks, cancellationToken);
    }

    sealed class TestJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
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

    sealed class TestProviderWidgetService : IAiProviderWidgetService
    {
        public Task<IReadOnlyList<AiProviderMetric>> RefreshMetricsAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiProviderMetric>>([]);

        public Task<IReadOnlyList<ManagedEndpointStatusView>> GetHuggingFaceStatusesAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ManagedEndpointStatusView>>([]);

        public Task<ManagedEndpointStatusView> ExecuteHuggingFaceActionAsync(AiProvider provider, AiProviderModel model, ManagedEndpointAction action, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    sealed class TestProviderConnectionService : IAiProviderConnectionService
    {
        public Task TestProviderAsync(AiProvider provider, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<List<AiProviderModel>> DiscoverModelsAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<AiProviderModel>());
    }

    sealed class TestProviderVoiceInventoryService : IAiProviderVoiceInventoryService
    {
        public bool IsRefreshing(AiProvider provider, AiProviderModel model) => false;

        public bool NeedsInitialRefresh(AiProviderModel model) => false;

        public Task<AiProviderVoiceRefreshResult> RefreshModelAsync(AiProvider provider, AiProviderModel model, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiProviderVoiceRefreshResult(model.Id, model.Id, true, ""));
    }

    sealed class TestElevenLabsVoiceCatalogService : IElevenLabsVoiceCatalogService
    {
        public Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            LoadSnapshotAsync(cancellationToken: cancellationToken);

        public Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default) =>
            LoadSnapshotAsync(cancellationToken: cancellationToken);

        public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            LoadSnapshotAsync(cancellationToken: cancellationToken);

        public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default) =>
            LoadSnapshotAsync(cancellationToken: cancellationToken);

        public Task<ElevenLabsVoiceCatalogSnapshot> LoadSnapshotAsync(ElevenLabsVoiceCatalogFilter? filter = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ElevenLabsVoiceCatalogSnapshot([], [], [], [], [], [], null, "", 0, 0));

        public Task<IReadOnlyList<AiProviderVoice>> LoadBookmarkedVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiProviderVoice>>([]);

        public Task SetBookmarkedAsync(string voiceId, bool bookmarked, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
