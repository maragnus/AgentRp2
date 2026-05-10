using AgentRp.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public static class StoryAudioEndpointExtensions
{
    public static IEndpointRouteBuilder MapStoryAudioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/story-audio/{voiceMessageId}", async (
            string voiceMessageId,
            HttpContext context,
            IDbContextFactory<RpDbContext> dbContextFactory,
            IAssetBlobStorage blobStorage,
            IVoiceMessageStreamCoordinator streamCoordinator,
            CancellationToken cancellationToken) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var audio = await dbContext.SpeechAssets
                .AsNoTracking()
                .Where(x => x.Id == voiceMessageId)
                .OrderBy(x => x.Id)
                .Select(x => new StoryAudioEndpointRow(
                    x.Status,
                    x.BlobName,
                    x.StoredByteLength,
                    x.ContentType,
                    x.FileName,
                    x.ErrorMessage,
                    x.CreatedUtc,
                    x.CompletedUtc))
                .FirstOrDefaultAsync(cancellationToken);

            if (audio is null)
                return Results.NotFound();

            if (audio.Status == SpeechAssetStatus.Ready)
                return await ServeReadyAudioAsync(voiceMessageId, context, blobStorage, audio, cancellationToken);

            if (audio.Status == SpeechAssetStatus.Failed)
                return Results.Problem(
                    detail: string.IsNullOrWhiteSpace(audio.ErrorMessage) ? "Reading aloud failed while generating the audio." : audio.ErrorMessage,
                    title: "Reading aloud failed",
                    statusCode: StatusCodes.Status500InternalServerError);

            var start = await streamCoordinator.EnsureStartedAsync(voiceMessageId, cancellationToken);
            if (!start.Started)
                return Results.Problem(
                    detail: start.ErrorMessage,
                    title: "Reading aloud failed",
                    statusCode: StatusCodes.Status500InternalServerError);

            context.Response.Headers.CacheControl = "no-store";
            return Results.Stream(
                stream => streamCoordinator.CopyLiveAsync(voiceMessageId, stream, context.RequestAborted),
                ResolveContentType(audio.ContentType),
                audio.FileName);
        });

        return endpoints;
    }

    static async Task<IResult> ServeReadyAudioAsync(
        string voiceMessageId,
        HttpContext context,
        IAssetBlobStorage blobStorage,
        StoryAudioEndpointRow audio,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(audio.BlobName) || audio.StoredByteLength <= 0)
            return Results.Problem(
                detail: "Reading aloud failed because the stored audio is missing.",
                title: "Reading aloud failed",
                statusCode: StatusCodes.Status500InternalServerError);

        var blob = await blobStorage.OpenReadAsync(audio.BlobName, cancellationToken);
        if (blob is null)
            return Results.NotFound();

        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        context.Response.Headers.ETag = $"\"{voiceMessageId}-{audio.StoredByteLength}-{(audio.CompletedUtc ?? audio.CreatedUtc).Ticks}\"";
        return Results.File(blob.Stream, ResolveContentType(audio.ContentType, blob.ContentType), audio.FileName, enableRangeProcessing: true);
    }

    static string ResolveContentType(string contentType, string fallback = "audio/mpeg") =>
        string.IsNullOrWhiteSpace(contentType) ? fallback : contentType;

    sealed record StoryAudioEndpointRow(
        string Status,
        string BlobName,
        long StoredByteLength,
        string ContentType,
        string FileName,
        string ErrorMessage,
        DateTime CreatedUtc,
        DateTime? CompletedUtc);
}
