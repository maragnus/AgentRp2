using AgentRp.Components.Common;
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

    [Fact]
    public async Task NewStoryModalVoiceModelSelectionCreatesStoryWhenNoStoryIsActive()
    {
        using var context = CreateContext(out _);
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync(selectFirstStory: false);
        await AddVoiceProviderAsync(session);
        var component = RenderModal(context, session);
        var overlays = context.Render<OverlayHost>();

        await ClickByText(component, "Enable Voice");
        await SelectVoiceModelAsync(component, overlays, "second-voice-model");
        await SelectNarratorVoiceAsync(component, overlays, "Second Voice");
        await ClickByText(component, "Create Story");

        var activeVoice = session.ActiveChat.Current?.ModelSelections.Values[AiModelRole.Voice];
        Assert.NotNull(activeVoice);
        Assert.Equal("voice-provider", activeVoice!.ProviderId);
        Assert.Equal("second-voice-model", activeVoice.ModelId);
        Assert.True(session.Chat.NarratorProfile.State.VoiceSelections.ContainsKey(ModelSelectionKey.Build("voice-provider", "second-voice-model")));
    }

    [Fact]
    public async Task NewStoryModalVoiceModelSelectionDoesNotMutateActiveStory()
    {
        using var context = CreateContext(out _);
        await using var store = NewLiveStore();
        var session = new RoleplaySession(store);
        await session.InitializeAsync();
        await AddVoiceProviderAsync(session);
        await session.ModelSelection.SetActiveModelAsync(AiModelRole.Voice, "voice-provider", "first-voice-model");
        var originalDocument = session.ActiveChat.Current!;
        var component = RenderModal(context, session);
        var overlays = context.Render<OverlayHost>();

        await ClickByText(component, "Enable Voice");
        await SelectVoiceModelAsync(component, overlays, "second-voice-model");

        var activeVoice = originalDocument.ModelSelections.Values[AiModelRole.Voice];
        Assert.Equal("voice-provider", activeVoice.ProviderId);
        Assert.Equal("first-voice-model", activeVoice.ModelId);
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
        context.Services.AddSingleton<ITtsPreviewService, TestPreviewService>();
        context.Services.AddSingleton<ITtsAudioPlaybackService, TestPlaybackService>();
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

    static async Task SelectVoiceModelAsync(IRenderedComponent<NewStoryModal> component, IRenderedComponent<OverlayHost> overlays, string modelId)
    {
        await component.Find(".voice-picker-provider-button").ClickAsync(new MouseEventArgs());
        var option = overlays.FindAll(".sidebar-model-option")
            .First(button => button.TextContent.Contains(modelId, StringComparison.Ordinal));
        await option.ClickAsync(new MouseEventArgs());
    }

    static async Task SelectNarratorVoiceAsync(IRenderedComponent<NewStoryModal> component, IRenderedComponent<OverlayHost> overlays, string voiceName)
    {
        await component.Find(".voice-picker-selected-button").ClickAsync(new MouseEventArgs());
        var option = overlays.FindAll(".voice-picker-option-main")
            .First(button => button.TextContent.Contains(voiceName, StringComparison.Ordinal));
        await option.ClickAsync(new MouseEventArgs());
    }

    static AngleSharp.Dom.IElement ButtonByText(IRenderedComponent<NewStoryModal> component, string text = "Create Story") =>
        component.FindAll("button").First(button => button.TextContent.Contains(text, StringComparison.Ordinal));

    static LiveRoleplayStore NewLiveStore() =>
        new(new SeedRoleplayPersistence(), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));

    static async Task AddVoiceProviderAsync(RoleplaySession session)
    {
        await session.Providers.AddAsync(new()
        {
            Id = "voice-provider",
            Name = "Voice Provider",
            Type = "elevenlabs",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "first-voice-model",
                    DisplayName = "First Voice Model",
                    Enabled = true,
                    Roles = [AiModelRole.Voice],
                    Capabilities = new() { TextInput = true, SpeechOutput = true },
                    Voices = [new() { Id = "first-voice", DisplayName = "First Voice" }]
                },
                new()
                {
                    Id = "second-voice-model",
                    DisplayName = "Second Voice Model",
                    Enabled = true,
                    Roles = [AiModelRole.Voice],
                    Capabilities = new() { TextInput = true, SpeechOutput = true },
                    Voices = [new() { Id = "second-voice", DisplayName = "Second Voice" }]
                }
            ]
        });
    }

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

    sealed class TestPreviewService : ITtsPreviewService
    {
        public Task<TtsPreviewAudio> GenerateSampleAsync(AiProvider provider, AiProviderModel model, AiProviderVoice voice, string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TtsPreviewAudio([], "audio/mpeg"));
    }

    sealed class TestPlaybackService : ITtsAudioPlaybackService
    {
        public string ActiveKey => "";
        public event Func<Task>? Changed;
        public event Func<string, string, Task>? Failed;
        public bool IsPlaying(string key) => false;
        public bool TryGetCachedUrl(string key, out string url)
        {
            url = "";
            return false;
        }

        public Task CacheAudioAsync(string key, byte[] bytes, string contentType) => Task.CompletedTask;
        public Task PlayUrlAsync(string key, string url) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public Task ReplaceCachedAudioAsync(string key, byte[] bytes, string contentType) => Task.CompletedTask;
        public Task NotifyAsync() => Changed?.Invoke() ?? Task.CompletedTask;
        public Task FailAsync(string key, string message) => Failed?.Invoke(key, message) ?? Task.CompletedTask;
    }
}
