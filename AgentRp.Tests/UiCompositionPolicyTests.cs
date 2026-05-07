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
using System.Runtime.CompilerServices;

namespace AgentRp.Tests;

public sealed class UiCompositionPolicyTests
{
    static readonly HashSet<string> InlineStyleAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        Normalize("AgentRp/Components/Common/Avatar.razor"),
        Normalize("AgentRp/Components/Common/EntityImage.razor"),
        Normalize("AgentRp/Components/Common/ImagePlaceholder.razor"),
        Normalize("AgentRp/Components/Common/ModalShell.razor"),
        Normalize("AgentRp/Components/Common/ModalSplitLayout.razor"),
        Normalize("AgentRp/Components/Common/ProgressBar.razor"),
        Normalize("AgentRp/Components/Common/RangeSlider.razor"),
        Normalize("AgentRp/Components/Common/TiledSelectList.razor")
    };

    [Fact]
    public void FeatureRazorFilesDoNotUseInlineStyleAttributes()
    {
        var root = FindRepoRoot();
        var violations = Directory
            .EnumerateFiles(Path.Combine(root, "AgentRp", "Components"), "*.razor", SearchOption.AllDirectories)
            .Where(path => !InlineStyleAllowList.Contains(Normalize(Path.GetRelativePath(root, path))))
            .Where(path => File.ReadAllText(path).Contains("style=", StringComparison.OrdinalIgnoreCase))
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .ToList();

        Assert.True(violations.Count == 0, $"Inline style attributes are only allowed in approved Core components: {string.Join(", ", violations)}");
    }

    [Fact]
    public void RazorComponentsDoNotExposeGenericStyleParameters()
    {
        var root = FindRepoRoot();
        var violations = Directory
            .EnumerateFiles(Path.Combine(root, "AgentRp", "Components"), "*.razor", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("[Parameter] public string? Style", StringComparison.Ordinal))
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .ToList();

        Assert.True(violations.Count == 0, $"Generic Style parameters are forbidden: {string.Join(", ", violations)}");
    }

    [Fact]
    public void FeatureRazorFilesDoNotUseModalPaneChromeClassesDirectly()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Normalize("AgentRp/Components/Common/ModalPaneActionBar.razor"),
            Normalize("AgentRp/Components/Common/ModalPaneHeader.razor"),
            Normalize("AgentRp/Components/Common/ModalPaneToolbar.razor")
        };
        var classNames = new[] { "modal-pane-head", "modal-pane-toolbar", "modal-pane-action-bar" };
        var violations = Directory
            .EnumerateFiles(Path.Combine(root, "AgentRp", "Components"), "*.razor", SearchOption.AllDirectories)
            .Where(path => !allowed.Contains(Normalize(Path.GetRelativePath(root, path))))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return classNames.Any(className => text.Contains(className, StringComparison.Ordinal));
            })
            .Select(path => Normalize(Path.GetRelativePath(root, path)))
            .ToList();

        Assert.True(violations.Count == 0, $"Use ModalPaneHeader/ModalPaneToolbar/ModalPaneActionBar instead of raw modal pane chrome classes: {string.Join(", ", violations)}");
    }

    [Fact]
    public async Task SidebarRendersRealSessionDataWithoutFakeMetrics()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<OverlayService>();
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
    public async Task SidebarModelPickerGroupsEnabledChatModelsByProvider()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
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

        var active = session.Chat.ModelSelection.Resolve(AiModelRole.Chat);
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
    public async Task EntityManagerWrapsSelectedHeaderInModalPaneChrome()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddScoped<OverlayService>();
        context.Services.AddSingleton<IElevenLabsVoiceCatalogService, TestElevenLabsVoiceCatalogService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<EntityManagerModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.InitialType, "characters")
            .Add(item => item.OnSelectEntityImage, _ => Task.CompletedTask)
            .Add(item => item.OnClose, () => Task.CompletedTask));

        Assert.NotNull(component.Find(".modal-stack-header .modal-pane-head .entity-form-head"));
    }

    [Fact]
    public void EntityListEditorAppliesHeaderChromeByDefault()
    {
        using var context = new BunitContext();
        var item = new ModalPolicyItem("one", "One");

        var component = context.Render<EntityListEditor<ModalPolicyItem>>(parameters => parameters
            .Add(value => value.Items, [item])
            .Add(value => value.SelectedId, item.Id)
            .Add(value => value.GetId, value => value.Id)
            .Add(value => value.ItemHeaderTemplate, value => builder =>
            {
                builder.OpenElement(0, "strong");
                builder.AddContent(1, value.Title);
                builder.CloseElement();
            })
            .Add(value => value.ItemTemplate, value => builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddContent(1, value.Title);
                builder.CloseElement();
            }));

        Assert.NotNull(component.Find(".modal-stack-header .modal-pane-head"));
    }

    [Fact]
    public async Task AiProvidersKeepsProviderHeaderAndActionBarOutsideScrollBody()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IJSRuntime, TestJsRuntime>();
        context.Services.AddScoped<OverlayService>();
        context.Services.AddSingleton<IModelCapabilityCatalog, TestModelCapabilityCatalog>();
        context.Services.AddSingleton<IAiProviderCapabilityPipeline, AiProviderCapabilityPipeline>();
        context.Services.AddSingleton<IAiProviderWidgetService, TestProviderWidgetService>();
        context.Services.AddSingleton<IAiProviderVoiceInventoryService, TestProviderVoiceInventoryService>();
        context.Services.AddScoped<IAiProviderConnectionService, TestProviderConnectionService>();
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = context.Render<AIProvidersModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(value => value.OnClose, () => Task.CompletedTask));

        Assert.NotNull(component.Find(".modal-stack-header .modal-pane-head.provider-detail-head"));
        Assert.NotNull(component.Find(".modal-stack-header .modal-pane-action-bar.provider-action-bar"));
        Assert.Empty(component.FindAll(".modal-stack-scroll .modal-pane-head.provider-detail-head"));
        Assert.Empty(component.FindAll(".modal-stack-scroll .modal-pane-action-bar.provider-action-bar"));
    }

    static LiveRoleplayStore NewLiveStore() =>
        new(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    static string FindRepoRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentRp.slnx")))
            directory = directory.Parent;

        if (directory is not null)
            return directory.FullName;

        directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentRp.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate AgentRp.slnx.");
    }

    static string Normalize(string path) => path.Replace('\\', '/');

    sealed record ModalPolicyItem(string Id, string Title);

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
