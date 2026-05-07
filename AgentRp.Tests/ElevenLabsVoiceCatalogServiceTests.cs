using System.Net;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Tests;

public sealed class ElevenLabsVoiceCatalogServiceTests
{
    [Fact]
    public async Task RefreshDownloadsAllPagesAndSortsAlphabetically()
    {
        var factory = NewFactory();
        var service = NewService(factory, new PagingHandler(request =>
        {
            var query = request.RequestUri?.Query ?? "";
            return query.Contains("page=0", StringComparison.Ordinal)
                ? Json("""
                    {
                      "voices": [
                        { "voice_id": "z", "name": "Zelda", "accent": "american", "gender": "Female", "age": "young", "use_case": "characters_animation", "category": "professional", "featured": false, "description": "Last", "preview_url": "https://example.com/z.mp3" }
                      ],
                      "has_more": true,
                      "total_count": 2
                    }
                    """)
                : Json("""
                    {
                      "voices": [
                        { "voice_id": "a", "name": "Ada", "accent": "british", "gender": "Female", "age": "adult", "use_case": "narration", "category": "high_quality", "featured": true, "description": "First", "preview_url": "https://example.com/a.mp3" }
                      ],
                      "has_more": false,
                      "total_count": 2
                    }
                    """);
        }));

        var snapshot = await service.RefreshAsync(Provider());

        Assert.Equal(["Ada", "Zelda"], snapshot.Voices.Select(voice => voice.Name));
        Assert.Equal(2, snapshot.TotalCount);
        Assert.Equal(2, snapshot.CachedCount);
        Assert.Equal(["american", "british"], snapshot.Accents);
    }

    [Fact]
    public async Task RefreshReportsPageProgress()
    {
        var factory = NewFactory();
        var progress = new TestProgress();
        var service = NewService(factory, new PagingHandler(request =>
        {
            var query = request.RequestUri?.Query ?? "";
            return query.Contains("page=0", StringComparison.Ordinal)
                ? Json("""
                    {
                      "voices": [
                        { "voice_id": "a", "name": "Ada" }
                      ],
                      "has_more": true,
                      "total_count": 121
                    }
                    """)
                : Json("""
                    {
                      "voices": [
                        { "voice_id": "b", "name": "Bram" }
                      ],
                      "has_more": false,
                      "total_count": 121
                    }
                    """);
        }));

        await service.RefreshAsync(Provider(), progress);

        Assert.Contains(progress.Items, item => item.CurrentPage == 1 && item.TotalPages == 2 && item.VoiceCount == 1);
        Assert.Contains(progress.Items, item => item.CurrentPage == 2 && item.TotalPages == 2 && item.VoiceCount == 2);
        Assert.Contains(progress.Items, item => item.Stage == "Saving");
    }


    [Fact]
    public async Task LoadSnapshotFiltersLocally()
    {
        var factory = NewFactory();
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.ElevenLabsVoiceCatalog.AddRange(
                Row("1", "Mara", featured: true, accent: "american", gender: "Female", age: "adult", useCase: "narration", category: "professional"),
                Row("2", "Bram", featured: false, accent: "british", gender: "Male", age: "middle_aged", useCase: "characters_animation", category: "high_quality"));
            await dbContext.SaveChangesAsync();
        }

        var service = NewService(factory, new PagingHandler(_ => Json("{}")));
        var snapshot = await service.LoadSnapshotAsync(new("search", "mara", true, "american", "Female", "adult", "narration", "professional"));

        var voice = Assert.Single(snapshot.Voices);
        Assert.Equal("Mara", voice.Name);
        Assert.Equal(["american", "british"], snapshot.Accents);
    }

    [Fact]
    public async Task RefreshPreservesBookmarksAndMarksMissingVoicesUnavailable()
    {
        var factory = NewFactory();
        await using (var dbContext = factory.CreateDbContext())
        {
            var oldRow = Row("old", "Old Voice");
            oldRow.IsBookmarked = true;
            dbContext.ElevenLabsVoiceCatalog.Add(oldRow);
            await dbContext.SaveChangesAsync();
        }

        var service = NewService(factory, new PagingHandler(_ => Json("""
            {
              "voices": [
                { "voice_id": "new", "name": "New Voice", "featured": false }
              ],
              "has_more": false,
              "total_count": 1
            }
            """)));

        await service.RefreshAsync(Provider());
        var bookmarked = await service.LoadSnapshotAsync(ElevenLabsVoiceCatalogFilter.Bookmarked);

        var old = Assert.Single(bookmarked.Voices);
        Assert.Equal("old", old.VoiceId);
        Assert.True(old.IsBookmarked);
        Assert.False(old.IsAvailable);
    }

    [Fact]
    public async Task SetBookmarkedTogglesLocalBookmark()
    {
        var factory = NewFactory();
        await using (var dbContext = factory.CreateDbContext())
        {
            dbContext.ElevenLabsVoiceCatalog.Add(Row("voice", "Voice"));
            await dbContext.SaveChangesAsync();
        }

        var service = NewService(factory, new PagingHandler(_ => Json("{}")));
        await service.SetBookmarkedAsync("voice", true);

        var bookmarks = await service.LoadBookmarkedVoicesAsync();
        var bookmark = Assert.Single(bookmarks);
        Assert.Equal("voice", bookmark.Id);
        Assert.True(bookmark.IsCatalogVoice);
        Assert.True(bookmark.IsBookmarked);
    }

    [Fact]
    public async Task RefreshStoresUserFacingErrorOnFailure()
    {
        var factory = NewFactory();
        var service = NewService(factory, new PagingHandler(_ => new(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"detail":"catalog unavailable"}""")
        }));

        var snapshot = await service.RefreshAsync(Provider());

        Assert.Contains("Refreshing the ElevenLabs voice catalog failed", snapshot.LastRefreshError, StringComparison.Ordinal);
        Assert.Contains("catalog unavailable", snapshot.LastRefreshError, StringComparison.Ordinal);
    }

    static TestDbContextFactory NewFactory() => new(Guid.NewGuid().ToString("N"));

    static ElevenLabsVoiceCatalogService NewService(TestDbContextFactory factory, HttpMessageHandler handler) =>
        new(factory, new TestHttpClientFactory(handler));

    static AiProvider Provider() => new()
    {
        Id = "elevenlabs",
        Name = "ElevenLabs",
        Type = "elevenlabs",
        ApiKey = "test-key"
    };

    static ElevenLabsVoiceCatalogRow Row(
        string voiceId,
        string name,
        bool featured = false,
        string accent = "",
        string gender = "",
        string age = "",
        string useCase = "",
        string category = "") => new()
    {
        VoiceId = voiceId,
        Name = name,
        Featured = featured,
        Accent = accent,
        Gender = gender,
        Age = age,
        UseCase = useCase,
        Category = category,
        CreatedUtc = DateTime.UtcNow,
        UpdatedUtc = DateTime.UtcNow,
        LastSeenUtc = DateTime.UtcNow,
        IsAvailable = true
    };

    static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    sealed class TestDbContextFactory(string databaseName) : IDbContextFactory<RpDbContext>
    {
        readonly DbContextOptions<RpDbContext> _options = new DbContextOptionsBuilder<RpDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        public RpDbContext CreateDbContext() => new(_options);
    }

    sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    sealed class PagingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    sealed class TestProgress : IProgress<ElevenLabsVoiceCatalogRefreshProgress>
    {
        public List<ElevenLabsVoiceCatalogRefreshProgress> Items { get; } = [];

        public void Report(ElevenLabsVoiceCatalogRefreshProgress value) => Items.Add(value);
    }
}
