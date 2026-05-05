using System.Text.Json.Nodes;
using AgentRp.Components.Common;
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
            Text = false,
            Image = true,
            Capabilities = new() { TextInput = true, TextOutput = true, ImageOutput = true }
        });

        var component = context.Render<Sidebar>(parameters => parameters
            .AddCascadingValue(session)
            .Add(item => item.OnOpenModal, _ => Task.CompletedTask)
            .Add(item => item.OnOpenEntities, _ => Task.CompletedTask));
        var overlays = context.Render<OverlayHost>();

        component.Find(".sidebar-model-summary-button").Click();

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
        component.Find(".sidebar-model-summary-button").Click();
        var option = overlays.FindAll(".sidebar-model-option")
            .First(button => button.TextContent.Contains("grok-4-0709", StringComparison.Ordinal));

        await option.ClickAsync(new());

        var active = TextModelTuningCatalog.TryResolveActiveTextModel(session.Providers.Items);
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

    static LiveRoleplayStore NewLiveStore() =>
        new(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgentRp.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate AgentRp.slnx.");
    }

    static string Normalize(string path) => path.Replace('\\', '/');

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
}
