using AgentRp.Data;
using AgentRp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentRp.Tests;

public sealed class StoredImageServiceTests
{
    static readonly byte[] PngBytes = [1, 2, 3];
    static readonly byte[] AvifBytes = [4, 5, 6];

    [Fact]
    public async Task StoreAsyncWithSuccessfulOptimizationStoresAvifBlobMetadata()
    {
        var dbFactory = new TestDbContextFactory();
        var blobStorage = new TestAssetBlobStorage();
        var optimizer = new TestImageOptimizer(new(AvifBytes, "image/avif", ".avif", true, true, "tinify", ""));
        var service = BuildService(dbFactory, blobStorage, optimizer);

        await service.StoreAsync(BuildRequest());

        Assert.True(blobStorage.Blobs.ContainsKey("images/chat-1/img-1.avif"));
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var row = await dbContext.ImageAssets.SingleAsync();
        Assert.Equal("images/chat-1/img-1.avif", row.BlobName);
        Assert.Equal("image/avif", row.StoredContentType);
        Assert.Equal("img-1.avif", row.StoredFileName);
        Assert.Equal("image/png", row.OriginalContentType);
        Assert.Equal(PngBytes.Length, row.OriginalByteLength);
        Assert.Equal(AvifBytes.Length, row.StoredByteLength);
        Assert.True(row.OptimizationAttempted);
        Assert.True(row.OptimizationSucceeded);
        Assert.Equal("tinify", row.OptimizationProvider);
        Assert.NotNull(row.OptimizedUtc);
    }

    [Fact]
    public async Task StoreAsyncWithOptimizationFailureStoresOriginalBlobMetadata()
    {
        var dbFactory = new TestDbContextFactory();
        var blobStorage = new TestAssetBlobStorage();
        var optimizer = new TestImageOptimizer(new(PngBytes, "image/png", ".png", true, false, "tinify", "Tinify failed."));
        var service = BuildService(dbFactory, blobStorage, optimizer);

        await service.StoreAsync(BuildRequest());

        Assert.True(blobStorage.Blobs.ContainsKey("images/chat-1/img-1.png"));
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var row = await dbContext.ImageAssets.SingleAsync();
        Assert.Equal("image/png", row.StoredContentType);
        Assert.Equal(PngBytes.Length, row.StoredByteLength);
        Assert.True(row.OptimizationAttempted);
        Assert.False(row.OptimizationSucceeded);
        Assert.Equal("tinify", row.OptimizationProvider);
        Assert.Equal("Tinify failed.", row.OptimizationError);
        Assert.Null(row.OptimizedUtc);
    }

    [Fact]
    public async Task StoreAsyncDeletesUploadedBlobWhenDatabaseSaveFails()
    {
        var blobStorage = new TestAssetBlobStorage();
        var service = BuildService(new ThrowingDbContextFactory(), blobStorage, new TestImageOptimizer());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StoreAsync(BuildRequest()));

        Assert.Contains("images/chat-1/img-1.png", blobStorage.DeletedBlobNames);
        Assert.Empty(blobStorage.Blobs);
    }

    static StoredImageService BuildService(
        IDbContextFactory<RpDbContext> dbFactory,
        TestAssetBlobStorage blobStorage,
        IImageOptimizer optimizer) =>
        new(dbFactory, optimizer, blobStorage, NullLogger<StoredImageService>.Instance);

    static StoreImageAssetRequest BuildRequest() => new(
        "chat-1",
        "img-1",
        PngBytes,
        "image/png",
        "image.png",
        "Image",
        1,
        1,
        "Prompt",
        "Final prompt",
        new(),
        "provider",
        "Provider",
        "model",
        DateTime.UtcNow);

    sealed class ThrowingDbContextFactory : IDbContextFactory<RpDbContext>
    {
        public RpDbContext CreateDbContext() =>
            throw new InvalidOperationException("Database save failed.");

        public Task<RpDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Database save failed.");
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
