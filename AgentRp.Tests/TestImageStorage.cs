using AgentRp.Services;

namespace AgentRp.Tests;

internal sealed class TestAssetBlobStorage : IAssetBlobStorage
{
    readonly Dictionary<string, (byte[] Bytes, string ContentType)> blobs = [];

    public IReadOnlyDictionary<string, (byte[] Bytes, string ContentType)> Blobs => blobs;
    public List<string> DeletedBlobNames { get; } = [];

    public Task UploadAsync(string blobName, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        blobs[blobName] = (bytes, contentType);
        return Task.CompletedTask;
    }

    public Task<StoredAssetBlob?> OpenReadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        if (!blobs.TryGetValue(blobName, out var blob))
            return Task.FromResult<StoredAssetBlob?>(null);

        return Task.FromResult<StoredAssetBlob?>(new(new MemoryStream(blob.Bytes), blob.ContentType));
    }

    public Task<byte[]> ReadBytesAsync(string blobName, CancellationToken cancellationToken = default) =>
        Task.FromResult(blobs[blobName].Bytes);

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        DeletedBlobNames.Add(blobName);
        blobs.Remove(blobName);
        return Task.CompletedTask;
    }
}

internal sealed class TestImageOptimizer(ImageOptimizationResult? result = null) : IImageOptimizer
{
    public List<ImageOptimizationRequest> Requests { get; } = [];

    public Task<ImageOptimizationResult> OptimizeAsync(ImageOptimizationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(result ?? ImageOptimizationResult.NotAttempted(
            request,
            ImageContentTypeRules.FileExtensionFor(request.ContentType, request.FileName)));
    }
}
