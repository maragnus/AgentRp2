using System.Collections.Concurrent;
using System.Threading.Channels;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public sealed record VoiceMessageGenerationRequest(
    string VoiceMessageId,
    AiProvider Provider,
    AiProviderModel Model,
    IReadOnlyList<SpeechGenerationInput> Inputs);

public sealed record VoiceMessageStartResult(bool Started, string ErrorMessage = "");

public interface IVoiceMessageStreamCoordinator
{
    void Start(VoiceMessageGenerationRequest request);

    Task<VoiceMessageStartResult> EnsureStartedAsync(
        string voiceMessageId,
        CancellationToken cancellationToken = default);

    Task CopyLiveAsync(
        string voiceMessageId,
        Stream output,
        CancellationToken cancellationToken = default);
}

public sealed class VoiceMessageStreamCoordinator(
    IDbContextFactory<RpDbContext> dbContextFactory,
    ISpeechGenerationService speechGenerationService,
    ILogger<VoiceMessageStreamCoordinator> logger) : IVoiceMessageStreamCoordinator
{
    readonly ConcurrentDictionary<string, LiveVoiceMessage> liveMessages = new(StringComparer.Ordinal);

    public void Start(VoiceMessageGenerationRequest request)
    {
        var live = liveMessages.GetOrAdd(request.VoiceMessageId, _ => new());
        lock (live.Gate)
        {
            if (live.ProducerTask is not null)
                return;

            live.ProducerTask = Task.Run(() => ProduceAsync(live, request));
        }
    }

    public async Task<VoiceMessageStartResult> EnsureStartedAsync(
        string voiceMessageId,
        CancellationToken cancellationToken = default)
    {
        if (IsProducing(voiceMessageId))
            return new(true);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.SpeechAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(asset => asset.Id == voiceMessageId, cancellationToken);
        if (row is null)
            return new(false, "Reading aloud failed because the audio was not found.");

        if (row.Status == SpeechAssetStatus.Ready)
            return new(true);
        if (row.Status == SpeechAssetStatus.Failed)
            return new(false, string.IsNullOrWhiteSpace(row.ErrorMessage)
                ? "Reading aloud failed while generating the audio."
                : row.ErrorMessage);

        var providerRow = await dbContext.AiProviders
            .AsNoTracking()
            .Include(provider => provider.Models)
            .Include(provider => provider.Metrics)
            .FirstOrDefaultAsync(provider => provider.Id == row.ProviderId, cancellationToken);
        if (providerRow is null)
            return await FailPendingAsync(row.Id, "Reading aloud failed because the voice provider no longer exists.", cancellationToken);

        var provider = AiProviderPersistenceMapper.ToModel(providerRow);
        var model = provider.Models.FirstOrDefault(model => model.Id == row.ProviderModelId);
        if (model is null)
            return await FailPendingAsync(row.Id, "Reading aloud failed because the voice model no longer exists.", cancellationToken);

        var inputs = SpeechGenerationInputJson.Deserialize(row.InputsJson);
        if (inputs.Count == 0)
            return await FailPendingAsync(row.Id, "Reading aloud failed because there was no text to read.", cancellationToken);

        Start(new(row.Id, provider, model, inputs));
        return new(true);
    }

    public async Task CopyLiveAsync(
        string voiceMessageId,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(voiceMessageId, cancellationToken);
        var live = liveMessages.GetOrAdd(voiceMessageId, _ => new());
        Channel<byte[]>? subscriber = null;
        List<byte[]> buffered;
        Exception? error;
        lock (live.Gate)
        {
            buffered = live.Chunks.ToList();
            error = live.Error;
            if (!live.Completed && error is null)
            {
                subscriber = Channel.CreateUnbounded<byte[]>(new()
                {
                    SingleReader = true,
                    SingleWriter = false
                });
                live.Subscribers.Add(subscriber);
            }
        }

        try
        {
            foreach (var chunk in buffered)
            {
                await output.WriteAsync(chunk, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            if (error is not null)
                throw error;

            if (subscriber is null)
                return;

            await foreach (var chunk in subscriber.Reader.ReadAllAsync(cancellationToken))
            {
                await output.WriteAsync(chunk, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (subscriber is not null)
            {
                lock (live.Gate)
                    live.Subscribers.Remove(subscriber);
            }
        }
    }

    async Task ProduceAsync(LiveVoiceMessage live, VoiceMessageGenerationRequest request)
    {
        var now = DateTime.UtcNow;
        try
        {
            await UpdateStatusAsync(request.VoiceMessageId, SpeechAssetStatus.Streaming, startedUtc: now);
            await using var audio = new MemoryStream();
            var contentType = "audio/mpeg";
            await foreach (var chunk in speechGenerationService.StreamAsync(request.Provider, request.Model, request.Inputs, CancellationToken.None))
            {
                if (chunk.Bytes.Length == 0)
                    continue;

                if (!string.IsNullOrWhiteSpace(chunk.ContentType))
                    contentType = chunk.ContentType;

                await audio.WriteAsync(chunk.Bytes);
                PublishChunk(live, chunk.Bytes);
            }

            var bytes = audio.ToArray();
            if (bytes.Length == 0)
                throw new InvalidOperationException("Generating speech failed because the service returned no audio.");

            await MarkReadyAsync(request.VoiceMessageId, bytes, contentType);
            Complete(live, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Generating voice message {VoiceMessageId} failed.", request.VoiceMessageId);
            var message = UserFacingErrorMessageBuilder.Build("Generating read-aloud failed.", exception);
            await MarkFailedAsync(request.VoiceMessageId, message);
            Complete(live, exception);
        }
        finally
        {
            liveMessages.TryRemove(request.VoiceMessageId, out _);
        }
    }

    void PublishChunk(LiveVoiceMessage live, byte[] chunk)
    {
        lock (live.Gate)
        {
            live.Chunks.Add(chunk);
            foreach (var subscriber in live.Subscribers.ToList())
                subscriber.Writer.TryWrite(chunk);
        }
    }

    void Complete(LiveVoiceMessage live, Exception? exception)
    {
        lock (live.Gate)
        {
            live.Completed = true;
            live.Error = exception;
            foreach (var subscriber in live.Subscribers.ToList())
            {
                if (exception is null)
                    subscriber.Writer.TryComplete();
                else
                    subscriber.Writer.TryComplete(exception);
            }

            live.Subscribers.Clear();
        }
    }

    bool IsProducing(string voiceMessageId)
    {
        if (!liveMessages.TryGetValue(voiceMessageId, out var live))
            return false;

        lock (live.Gate)
            return live.ProducerTask is not null && !live.Completed && live.Error is null;
    }

    async Task<VoiceMessageStartResult> FailPendingAsync(
        string voiceMessageId,
        string message,
        CancellationToken cancellationToken)
    {
        await MarkFailedAsync(voiceMessageId, message, cancellationToken);
        return new(false, message);
    }

    async Task UpdateStatusAsync(
        string voiceMessageId,
        string status,
        DateTime? startedUtc = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.SpeechAssets.FirstOrDefaultAsync(asset => asset.Id == voiceMessageId, cancellationToken);
        if (row is null)
            return;

        row.Status = status;
        row.StartedUtc = startedUtc ?? row.StartedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    async Task MarkReadyAsync(string voiceMessageId, byte[] bytes, string contentType)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var row = await dbContext.SpeechAssets.FirstOrDefaultAsync(asset => asset.Id == voiceMessageId);
        if (row is null)
            return;

        row.Status = SpeechAssetStatus.Ready;
        row.Bytes = bytes;
        row.ContentType = string.IsNullOrWhiteSpace(contentType) ? "audio/mpeg" : contentType;
        row.ErrorMessage = "";
        row.CompletedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    async Task MarkFailedAsync(
        string voiceMessageId,
        string message,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.SpeechAssets.FirstOrDefaultAsync(asset => asset.Id == voiceMessageId, cancellationToken);
        if (row is null)
            return;

        row.Status = SpeechAssetStatus.Failed;
        row.ErrorMessage = UserFacingErrorMessageBuilder.Sanitize(message);
        row.CompletedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    sealed class LiveVoiceMessage
    {
        public object Gate { get; } = new();
        public List<byte[]> Chunks { get; } = [];
        public List<Channel<byte[]>> Subscribers { get; } = [];
        public Task? ProducerTask { get; set; }
        public bool Completed { get; set; }
        public Exception? Error { get; set; }
    }
}
