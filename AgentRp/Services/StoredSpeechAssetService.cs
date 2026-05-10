using AgentRp.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public interface IStoredSpeechAssetService
{
    Task StoreReadyAsync(string voiceMessageId, byte[] bytes, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string voiceMessageId, CancellationToken cancellationToken = default);
}

public sealed class StoredSpeechAssetService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IAssetBlobStorage blobStorage,
    ILogger<StoredSpeechAssetService> logger) : IStoredSpeechAssetService
{
    public async Task StoreReadyAsync(
        string voiceMessageId,
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (bytes.Length == 0)
            throw new InvalidOperationException("Saving read-aloud audio failed because the generated audio was empty.");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.SpeechAssets
            .OrderBy(asset => asset.Id)
            .FirstOrDefaultAsync(asset => asset.Id == voiceMessageId, cancellationToken);
        if (row is null)
            return;

        var storedContentType = string.IsNullOrWhiteSpace(contentType) ? "audio/mpeg" : contentType;
        var blobName = BuildBlobName(row.ChatId, row.Id);

        await blobStorage.UploadAsync(blobName, bytes, storedContentType, cancellationToken);
        try
        {
            row.Status = SpeechAssetStatus.Ready;
            row.BlobName = blobName;
            row.StoredByteLength = bytes.LongLength;
            row.ContentType = storedContentType;
            row.ErrorMessage = "";
            row.CompletedUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteBlobAsync(blobName, cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(string voiceMessageId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.SpeechAssets
            .OrderBy(asset => asset.Id)
            .FirstOrDefaultAsync(asset => asset.Id == voiceMessageId, cancellationToken);
        if (row is null)
            return;

        var blobName = row.BlobName;
        dbContext.SpeechAssets.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(blobName))
            await TryDeleteBlobAsync(blobName, cancellationToken);
    }

    async Task TryDeleteBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await blobStorage.DeleteAsync(blobName, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Deleting speech blob {BlobName} failed.", blobName);
        }
    }

    static string BuildBlobName(string chatId, string voiceMessageId) =>
        $"audio/{chatId}/{voiceMessageId}";
}
