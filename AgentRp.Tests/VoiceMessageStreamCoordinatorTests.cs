using System.Runtime.CompilerServices;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentRp.Tests;

public sealed class VoiceMessageStreamCoordinatorTests
{
    [Fact]
    public async Task CopyLiveAsyncCatchesUpWithBufferedChunks()
    {
        var dbFactory = CreateFactory();
        await SeedPendingVoiceMessageAsync(dbFactory, "speech-1");
        var speech = new ControlledSpeechGenerationService();
        var coordinator = BuildCoordinator(dbFactory, speech);
        coordinator.Start(Request("speech-1"));
        await speech.FirstChunkConsumed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using var output = new MemoryStream();
        var copy = coordinator.CopyLiveAsync("speech-1", output);
        speech.Continue.SetResult();
        await copy.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1, 2], output.ToArray());
    }

    [Fact]
    public async Task EnsureStartedAsyncStartsOnlyOneProducer()
    {
        var dbFactory = CreateFactory();
        await SeedPendingVoiceMessageAsync(dbFactory, "speech-1");
        var speech = new ControlledSpeechGenerationService();
        var blobStorage = new TestAssetBlobStorage();
        var coordinator = BuildCoordinator(dbFactory, speech, blobStorage);

        var first = await coordinator.EnsureStartedAsync("speech-1");
        var second = await coordinator.EnsureStartedAsync("speech-1");
        speech.Continue.SetResult();
        await WaitForStatusAsync(dbFactory, "speech-1", SpeechAssetStatus.Ready);

        Assert.True(first.Started);
        Assert.True(second.Started);
        Assert.Equal(1, speech.Calls);
        Assert.Equal([1, 2], blobStorage.Blobs["audio/chat-1/speech-1"].Bytes);
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var row = await dbContext.SpeechAssets.AsNoTracking().SingleAsync(asset => asset.Id == "speech-1");
        Assert.Equal("audio/chat-1/speech-1", row.BlobName);
        Assert.Equal(2, row.StoredByteLength);
        Assert.Equal("audio/mpeg", row.ContentType);
    }

    [Fact]
    public async Task ProducerFailureMarksVoiceMessageFailed()
    {
        var dbFactory = CreateFactory();
        await SeedPendingVoiceMessageAsync(dbFactory, "speech-1");
        var speech = new ControlledSpeechGenerationService { Throw = true };
        var coordinator = BuildCoordinator(dbFactory, speech);

        coordinator.Start(Request("speech-1"));
        await WaitForStatusAsync(dbFactory, "speech-1", SpeechAssetStatus.Failed);

        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var row = await dbContext.SpeechAssets.AsNoTracking().SingleAsync(asset => asset.Id == "speech-1");
        Assert.Contains("Generating read-aloud failed", row.ErrorMessage, StringComparison.Ordinal);
    }

    static VoiceMessageGenerationRequest Request(string id) => new(
        id,
        new() { Id = "provider-1", Name = "Provider", Type = "openai" },
        new() { Id = "model-1" },
        [new("Hello", "voice-1")]);

    static async Task SeedPendingVoiceMessageAsync(TestDbContextFactory dbFactory, string id)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.AiProviders.Add(new()
        {
            Id = "provider-1",
            Name = "Provider",
            Type = "openai",
            Enabled = true,
            Models =
            [
                new()
                {
                    Id = "model-1",
                    Enabled = true,
                    RolesJson = "[\"voice\"]",
                    VoicesJson = "[]"
                }
            ]
        });
        dbContext.SpeechAssets.Add(new()
        {
            Id = id,
            ChatId = "chat-1",
            TurnId = "turn-1",
            Status = SpeechAssetStatus.Pending,
            ContentType = "audio/mpeg",
            FileName = "turn-1.mp3",
            ProviderId = "provider-1",
            ProviderName = "Provider",
            ProviderType = "openai",
            ProviderModelId = "model-1",
            SourceHash = "hash",
            InputsJson = """[{"text":"Hello","voiceId":"voice-1"}]""",
            CreatedUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    static async Task WaitForStatusAsync(TestDbContextFactory dbFactory, string id, string status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            await using var dbContext = await dbFactory.CreateDbContextAsync();
            var current = await dbContext.SpeechAssets
                .AsNoTracking()
                .Where(asset => asset.Id == id)
                .Select(asset => asset.Status)
                .SingleAsync(timeout.Token);
            if (current == status)
                return;

            await Task.Delay(20, timeout.Token);
        }
    }

    static TestDbContextFactory CreateFactory() => new();

    static VoiceMessageStreamCoordinator BuildCoordinator(
        TestDbContextFactory dbFactory,
        ISpeechGenerationService speechGenerationService,
        TestAssetBlobStorage? blobStorage = null)
    {
        var storedSpeechAssetService = new StoredSpeechAssetService(
            dbFactory,
            blobStorage ?? new TestAssetBlobStorage(),
            NullLogger<StoredSpeechAssetService>.Instance);
        return new(
            dbFactory,
            speechGenerationService,
            storedSpeechAssetService,
            NullLogger<VoiceMessageStreamCoordinator>.Instance);
    }

    sealed class ControlledSpeechGenerationService : ISpeechGenerationService
    {
        public TaskCompletionSource FirstChunkConsumed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public bool Throw { get; set; }

        public async IAsyncEnumerable<SpeechAudioChunk> StreamAsync(
            AiProvider provider,
            AiProviderModel model,
            IReadOnlyList<SpeechGenerationInput> inputs,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Throw)
                throw new InvalidOperationException("provider failed");

            yield return new([1], "audio/mpeg");
            FirstChunkConsumed.TrySetResult();
            await Continue.Task.WaitAsync(cancellationToken);
            yield return new([2], "audio/mpeg");
        }

        public async Task<SpeechAudio> GenerateAsync(
            AiProvider provider,
            AiProviderModel model,
            IReadOnlyList<SpeechGenerationInput> inputs,
            CancellationToken cancellationToken = default)
        {
            await using var output = new MemoryStream();
            await foreach (var chunk in StreamAsync(provider, model, inputs, cancellationToken))
                await output.WriteAsync(chunk.Bytes, cancellationToken);

            return new(output.ToArray(), "audio/mpeg");
        }
    }

    sealed class TestDbContextFactory : IDbContextFactory<RpDbContext>
    {
        readonly DbContextOptions<RpDbContext> options = new DbContextOptionsBuilder<RpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        public RpDbContext CreateDbContext() => new(options);

        public Task<RpDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
