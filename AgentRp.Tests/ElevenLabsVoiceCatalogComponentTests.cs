using AgentRp.Components.Providers;
using AgentRp.Models;
using AgentRp.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace AgentRp.Tests;

public sealed class ElevenLabsVoiceCatalogComponentTests
{
    [Fact]
    public void MergeKeepsProviderVoiceDataAndAddsBookmarkedCatalogVoices()
    {
        var provider = new AiProviderVoice
        {
            Id = "same",
            DisplayName = "Provider Name",
            Description = "Provider description",
            Source = "elevenlabs"
        };
        var catalogSame = new AiProviderVoice
        {
            Id = "same",
            DisplayName = "Catalog Name",
            PreviewUrl = "https://example.com/same.mp3",
            Source = "elevenlabs-catalog",
            IsCatalogVoice = true,
            IsBookmarked = true,
            Labels = new(StringComparer.OrdinalIgnoreCase) { ["accent"] = "American" }
        };
        var catalogOnly = new AiProviderVoice
        {
            Id = "catalog-only",
            DisplayName = "Ada",
            Source = "elevenlabs-catalog",
            IsCatalogVoice = true,
            IsBookmarked = true
        };

        var merged = AiProviderVoiceMergeRules.MergeProviderAndCatalogVoices([provider], [catalogSame, catalogOnly]);

        Assert.Equal(["Ada", "Provider Name"], merged.Select(voice => voice.DisplayName));
        var duplicate = merged.Single(voice => voice.Id == "same");
        Assert.Equal("Provider Name", duplicate.DisplayName);
        Assert.Equal("Provider description", duplicate.Description);
        Assert.Equal("https://example.com/same.mp3", duplicate.PreviewUrl);
        Assert.True(duplicate.IsCatalogVoice);
        Assert.True(duplicate.IsBookmarked);
        Assert.Equal("American", duplicate.Labels["accent"]);
    }

    [Fact]
    public void VoiceCatalogRowShowsRequiredCatalogFieldsAndBookmarkAction()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddSingleton<ITtsPreviewService, TestPreviewService>();
        context.Services.AddSingleton<ITtsAudioPlaybackService, TestPlaybackService>();

        var voice = new ElevenLabsVoiceCatalogEntry
        {
            VoiceId = "voice",
            Name = "Ada",
            Description = "Warm narration voice.",
            PreviewUrl = "https://example.com/ada.mp3",
            Featured = true,
            Accent = "american",
            Gender = "Female",
            Age = "adult",
            UseCase = "narration",
            Category = "professional",
            IsBookmarked = true
        };

        var component = context.Render<ElevenLabsVoiceCatalogRow>(parameters => parameters
            .Add(value => value.Provider, new AiProvider { Id = "p", Type = "elevenlabs", Name = "ElevenLabs" })
            .Add(value => value.PreviewModel, new AiProviderModel { Id = "eleven_multilingual_v2" })
            .Add(value => value.Voice, voice));

        Assert.Contains("Ada", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Warm narration voice.", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Featured", component.Markup, StringComparison.Ordinal);
        Assert.Contains("American", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Female", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Adult", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Narration", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Professional", component.Markup, StringComparison.Ordinal);
        Assert.Contains("fa-circle-play", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Remove Ada bookmark", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceCatalogModalShowsOnlyTwentyRowsAtATime()
    {
        using var context = NewContext();
        var service = new TestCatalogService(Enumerable.Range(1, 25)
            .Select(index => Voice($"v{index:00}", $"Voice {index:00}"))
            .ToList());
        context.Services.AddSingleton<IElevenLabsVoiceCatalogService>(service);

        var component = RenderModal(context);
        component.WaitForAssertion(() => Assert.Contains("Catalog", component.Markup, StringComparison.Ordinal));
        component.FindAll("button").First(button => button.TextContent.Contains("Catalog", StringComparison.Ordinal)).Click();

        component.WaitForAssertion(() =>
        {
            Assert.Equal(20, component.FindAll(".voice-catalog-row").Count);
            Assert.Contains("Showing 1-20 of 25", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void VoiceCatalogRefreshRequiresConfirmation()
    {
        using var context = NewContext();
        context.Services.AddSingleton<IElevenLabsVoiceCatalogService>(new TestCatalogService([Voice("v1", "Ada")]));

        var component = RenderModal(context);
        component.WaitForAssertion(() => Assert.Contains("Refresh", component.Markup, StringComparison.Ordinal));
        component.FindAll("button").First(button => button.TextContent.Contains("Refresh", StringComparison.Ordinal)).Click();

        Assert.Contains("Refresh ElevenLabs voice catalog?", component.Markup, StringComparison.Ordinal);
        Assert.Contains("may take several minutes", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceCatalogBookmarkViewUpdatesAfterBookmarkToggle()
    {
        using var context = NewContext();
        var service = new TestCatalogService([Voice("v1", "Ada")]);
        context.Services.AddSingleton<IElevenLabsVoiceCatalogService>(service);

        var component = RenderModal(context);
        component.WaitForAssertion(() => Assert.Contains("Catalog", component.Markup, StringComparison.Ordinal));
        component.FindAll("button").First(button => button.TextContent.Contains("Catalog", StringComparison.Ordinal)).Click();
        component.WaitForAssertion(() => Assert.Contains("Bookmark Ada", component.Markup, StringComparison.Ordinal));
        component.Find("button[title='Bookmark Ada']").Click();
        component.FindAll("button").First(button => button.TextContent.Contains("Bookmarked", StringComparison.Ordinal)).Click();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Ada", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Remove Ada bookmark", component.Markup, StringComparison.Ordinal);
        });
    }

    static BunitContext NewContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<ITtsPreviewService, TestPreviewService>();
        context.Services.AddSingleton<ITtsAudioPlaybackService, TestPlaybackService>();
        return context;
    }

    static IRenderedComponent<ElevenLabsVoiceCatalogModal> RenderModal(BunitContext context) =>
        context.Render<ElevenLabsVoiceCatalogModal>(parameters => parameters
            .Add(value => value.Provider, new AiProvider { Id = "p", Type = "elevenlabs", Name = "ElevenLabs" })
            .Add(value => value.PreviewModel, new AiProviderModel { Id = "eleven_multilingual_v2" })
            .Add(value => value.OnClose, () => Task.CompletedTask));

    static ElevenLabsVoiceCatalogEntry Voice(string id, string name) => new()
    {
        VoiceId = id,
        Name = name,
        Description = $"{name} description",
        IsAvailable = true,
        UpdatedUtc = DateTime.UtcNow
    };

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

    sealed class TestCatalogService(List<ElevenLabsVoiceCatalogEntry> voices) : IElevenLabsVoiceCatalogService
    {
        public Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            LoadSnapshotAsync(cancellationToken: cancellationToken);

        public Task<ElevenLabsVoiceCatalogSnapshot> EnsureLoadedAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default) =>
            LoadSnapshotAsync(cancellationToken: cancellationToken);

        public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            LoadSnapshotAsync(cancellationToken: cancellationToken);

        public Task<ElevenLabsVoiceCatalogSnapshot> RefreshAsync(AiProvider provider, IProgress<ElevenLabsVoiceCatalogRefreshProgress> progress, CancellationToken cancellationToken = default)
        {
            progress.Report(new(1, 1, voices.Count, voices.Count, "Complete"));
            return LoadSnapshotAsync(cancellationToken: cancellationToken);
        }

        public Task<ElevenLabsVoiceCatalogSnapshot> LoadSnapshotAsync(ElevenLabsVoiceCatalogFilter? filter = null, CancellationToken cancellationToken = default)
        {
            var filtered = string.Equals(filter?.View, "bookmarked", StringComparison.OrdinalIgnoreCase)
                ? voices.Where(voice => voice.IsBookmarked)
                : voices;
            if (!string.IsNullOrWhiteSpace(filter?.Search))
                filtered = filtered.Where(voice => voice.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(new ElevenLabsVoiceCatalogSnapshot(
                filtered.OrderBy(voice => voice.Name).ToList(),
                [], [], [], [], [],
                DateTime.UtcNow,
                "",
                voices.Count,
                voices.Count));
        }

        public Task<IReadOnlyList<AiProviderVoice>> LoadBookmarkedVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AiProviderVoice>>(voices.Where(voice => voice.IsBookmarked).Select(ElevenLabsVoiceCatalogService.ToProviderVoice).ToList());

        public Task SetBookmarkedAsync(string voiceId, bool bookmarked, CancellationToken cancellationToken = default)
        {
            var voice = voices.FirstOrDefault(voice => voice.VoiceId == voiceId);
            if (voice is not null)
                voice.IsBookmarked = bookmarked;

            return Task.CompletedTask;
        }
    }
}
