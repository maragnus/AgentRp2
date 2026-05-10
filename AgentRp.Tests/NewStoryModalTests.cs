using AgentRp.Components.Shell;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRp.Tests;

public sealed class NewStoryModalTests
{
    [Fact]
    public async Task NewStoryModalLoadsRememberedTtsChoice()
    {
        using var context = CreateContext(out var settings);
        await settings.SaveAsync(NewStoryPreferencesState.SettingsKey, new NewStoryPreferencesState(true));
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();

        var component = RenderModal(context, session);

        component.WaitForAssertion(() => Assert.Contains("Auto Speak", component.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NewStoryModalPersistsTtsChoice()
    {
        using var context = CreateContext(out var settings);
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        var component = RenderModal(context, session);

        await ClickByText(component, "Enable Voice");

        var saved = await settings.GetAsync(NewStoryPreferencesState.SettingsKey, NewStoryPreferencesState.Default);
        Assert.True(saved.EnableTts);
    }

    [Fact]
    public async Task NewStoryModalRequiresNarratorVoiceWhenVoiceChoicesExist()
    {
        using var context = CreateContext(out var settings);
        await settings.SaveAsync(NewStoryPreferencesState.SettingsKey, new NewStoryPreferencesState(true));
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        session.Providers.Items.First().Models.Insert(0, new()
        {
            Id = "voice-model",
            DisplayName = "Voice Model",
            Enabled = true,
            Roles = [AiModelRole.Voice],
            Capabilities = new() { TextInput = true, SpeechOutput = true },
            Voices = [new() { Id = "narrator-voice", DisplayName = "Narrator Voice" }]
        });

        var component = RenderModal(context, session);

        component.WaitForAssertion(() => Assert.True(ButtonByText(component).HasAttribute("disabled")));
    }

    [Fact]
    public async Task NewStoryModalAutoSpeakSelectionPersistsToCreatedStory()
    {
        using var context = CreateContext(out _);
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        var component = RenderModal(context, session);

        await ClickByText(component, "Enable Voice");
        await ClickByText(component, "Auto Speak");
        await ClickByText(component, "Create Story");

        Assert.True(session.Chat.Transcript.Options.AutoSpeakNewMessages);
    }

    static BunitContext CreateContext(out InMemoryAppSettingsService settings)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        settings = new();
        context.Services.AddSingleton<IAppSettingsService>(settings);
        context.Services.AddScoped<OverlayService>();
        context.Services.AddSingleton<IModelSelectionNotifier, ModelSelectionNotifier>();
        context.Services.AddSingleton<IElevenLabsVoiceCatalogService, EmptyElevenLabsVoiceCatalogService>();
        return context;
    }

    static IRenderedComponent<NewStoryModal> RenderModal(BunitContext context, RoleplaySession session) =>
        context.Render<NewStoryModal>(parameters => parameters
            .AddCascadingValue(session)
            .Add(component => component.OnClose, () => Task.CompletedTask)
            .Add(component => component.OnOpenAssistant, () => Task.CompletedTask));

    static async Task ClickByText(IRenderedComponent<NewStoryModal> component, string text)
    {
        await ButtonByText(component, text).ClickAsync(new MouseEventArgs());
    }

    static AngleSharp.Dom.IElement ButtonByText(IRenderedComponent<NewStoryModal> component, string text = "Create Story") =>
        component.FindAll("button").First(button => button.TextContent.Contains(text, StringComparison.Ordinal));

    static LiveRoleplayStore NewLiveStore() =>
        new(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    sealed class EmptyElevenLabsVoiceCatalogService : IElevenLabsVoiceCatalogService
    {
        public Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptySnapshot());

        public Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptySnapshot());

        public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptySnapshot());

        public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptySnapshot());

        public Task<ElevenLabsVoiceCatalogSnapshot> LoadSnapshotAsync(ElevenLabsVoiceCatalogFilter? filter = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(EmptySnapshot());

        public Task<IReadOnlyList<AiProviderVoice>> LoadBookmarkedVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiProviderVoice>>([]);

        public Task SetBookmarkedAsync(string voiceId, bool bookmarked, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        static ElevenLabsVoiceCatalogSnapshot EmptySnapshot() =>
            new([], [], [], [], [], [], null, "", 0, 0);
    }
}
