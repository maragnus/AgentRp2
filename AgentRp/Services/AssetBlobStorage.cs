using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AgentRp.Services;

public sealed record StoredAssetBlob(Stream Stream, string ContentType);

public interface IAssetBlobStorage
{
    Task UploadAsync(string blobName, byte[] bytes, string contentType, CancellationToken cancellationToken = default);
    Task<StoredAssetBlob?> OpenReadAsync(string blobName, CancellationToken cancellationToken = default);
    Task<byte[]> ReadBytesAsync(string blobName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);
}

public sealed class AzureAssetBlobStorage(BlobContainerClient containerClient) : IAssetBlobStorage
{
    public async Task UploadAsync(string blobName, byte[] bytes, string contentType, CancellationToken cancellationToken = default)
    {
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = containerClient.GetBlobClient(blobName);
        await using var stream = new MemoryStream(bytes);
        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
                }
            },
            cancellationToken);
    }

    public async Task<StoredAssetBlob?> OpenReadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = containerClient.GetBlobClient(blobName);
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            var stream = await blob.OpenReadAsync(new BlobOpenReadOptions(false), cancellationToken);
            return new(stream, properties.Value.ContentType);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadBytesAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blob = containerClient.GetBlobClient(blobName);
        var response = await blob.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToArray();
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var blob = containerClient.GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
