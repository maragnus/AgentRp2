using System.Text.Json;
using AgentRp.Data;
using AgentRp.Serialization;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public sealed record StoreImageAssetRequest(
    string ChatId,
    Guid UserId,
    string ImageId,
    byte[] Bytes,
    string ContentType,
    string FileName,
    string Title,
    string Entity,
    string EntityType,
    int Hue,
    int? Width,
    int? Height,
    string UserPrompt,
    string FinalPrompt,
    ImageAssetGenerationMetadata GenerationMetadata,
    string ProviderId,
    string ProviderName,
    string ProviderModelId,
    DateTime CreatedUtc);

public sealed record StoredImageContent(byte[] Bytes, string ContentType);

public interface IStoredImageService
{
    Task StoreAsync(StoreImageAssetRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredImageContent>> LoadContentAsync(Guid userId, IReadOnlyCollection<string> imageIds, CancellationToken cancellationToken = default);
}

public sealed class StoredImageService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IImageOptimizer imageOptimizer,
    IAssetBlobStorage blobStorage,
    ILogger<StoredImageService> logger) : IStoredImageService
{
    public async Task StoreAsync(StoreImageAssetRequest request, CancellationToken cancellationToken = default)
    {
        var optimization = await imageOptimizer.OptimizeAsync(new(request.Bytes, request.ContentType, request.FileName), cancellationToken);
        var storedFileName = BuildStoredFileName(request.ImageId, optimization.FileExtension);
        var blobName = BuildBlobName(request.UserId, request.ChatId, request.ImageId, optimization.FileExtension);

        await blobStorage.UploadAsync(blobName, optimization.Bytes, optimization.ContentType, cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.ImageAssets.Add(new ImageAssetRow
            {
                Id = request.ImageId,
                ChatId = request.ChatId,
                UserId = request.UserId,
                BlobName = blobName,
                StoredContentType = optimization.ContentType,
                StoredFileName = storedFileName,
                OriginalContentType = request.ContentType,
                OriginalByteLength = request.Bytes.LongLength,
                StoredByteLength = optimization.Bytes.LongLength,
                OptimizationAttempted = optimization.Attempted,
                OptimizationSucceeded = optimization.Succeeded,
                OptimizationProvider = optimization.Provider,
                OptimizationError = optimization.ErrorMessage,
                OptimizedUtc = optimization.Succeeded ? request.CreatedUtc : null,
                Title = request.Title,
                Entity = request.Entity,
                EntityType = request.EntityType,
                Hue = request.Hue,
                Width = request.Width,
                Height = request.Height,
                UserPrompt = request.UserPrompt,
                FinalPrompt = request.FinalPrompt,
                GenerationMetadataJson = JsonSerializer.Serialize(request.GenerationMetadata, AppJsonSerializerOptions.Web),
                ProviderId = request.ProviderId,
                ProviderName = request.ProviderName,
                ProviderModelId = request.ProviderModelId,
                CreatedUtc = request.CreatedUtc
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteUploadedBlobAsync(blobName, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<StoredImageContent>> LoadContentAsync(Guid userId, IReadOnlyCollection<string> imageIds, CancellationToken cancellationToken = default)
    {
        if (imageIds.Count == 0)
            return [];

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.ImageAssets
            .AsNoTracking()
            .Where(image => image.UserId == userId && imageIds.Contains(image.Id))
            .Select(image => new { image.Id, image.BlobName, image.StoredContentType })
            .ToListAsync(cancellationToken);
        var rowsById = rows.ToDictionary(row => row.Id, StringComparer.Ordinal);

        var contents = new List<StoredImageContent>();
        foreach (var imageId in imageIds)
        {
            if (!rowsById.TryGetValue(imageId, out var row))
                continue;

            var bytes = await blobStorage.ReadBytesAsync(row.BlobName, cancellationToken);
            contents.Add(new(bytes, row.StoredContentType));
        }

        return contents;
    }

    async Task TryDeleteUploadedBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await blobStorage.DeleteAsync(blobName, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Deleting image blob {BlobName} after a failed database save failed.", blobName);
        }
    }

    static string BuildBlobName(Guid userId, string chatId, string imageId, string extension) =>
        string.IsNullOrWhiteSpace(chatId)
            ? $"images/users/{userId:N}/{imageId}{extension}"
            : $"images/users/{userId:N}/stories/{chatId}/{imageId}{extension}";

    static string BuildStoredFileName(string imageId, string extension) =>
        $"{imageId}{extension}";
}
