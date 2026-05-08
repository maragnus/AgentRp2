using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;
using AgentRp.Session;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Tests;

public sealed class ImageCropServiceTests
{
    static readonly byte[] PngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    [Fact]
    public async Task GetAsyncUsesDefaultCropWhenNoCropHasBeenSaved()
    {
        var dbFactory = new TestDbContextFactory();
        await SeedImageAssetAsync(dbFactory, "chat-1", "img-1");
        var service = new ImageCropService(dbFactory);
        var document = new RpChatDocument
        {
            Chat = new() { Id = "chat-1" },
            Images =
            [
                new() { Id = "img-1", Name = "Gemma", Url = "/story-images/img-1" }
            ]
        };

        var view = await service.GetAsync(document, "img-1");

        Assert.Equal(ImageAvatarCropView.Default, view.Crop);
        Assert.Equal("/story-images/img-1", view.ImageUrl);
    }

    [Fact]
    public async Task UpdateAsyncClampsAndPersistsCropToImageAsset()
    {
        var dbFactory = new TestDbContextFactory();
        await SeedImageAssetAsync(dbFactory, "chat-1", "img-1");
        var service = new ImageCropService(dbFactory);

        var crop = await service.UpdateAsync(new("chat-1", "img-1", -10, 120, 450));

        Assert.Equal(new ImageAvatarCropView(0, 100, 300), crop);
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        var row = await dbContext.ImageAssets.SingleAsync();
        Assert.Equal(0, row.AvatarFocusXPercent);
        Assert.Equal(100, row.AvatarFocusYPercent);
        Assert.Equal(300, row.AvatarZoomPercent);
    }

    [Fact]
    public void GalleryImageDefaultsToAgentRp1CropDefaults()
    {
        var image = new GalleryImage();

        Assert.Equal(50, image.AvatarFocusXPercent);
        Assert.Equal(50, image.AvatarFocusYPercent);
        Assert.Equal(100, image.AvatarZoomPercent);
        Assert.Equal(ImageAvatarCropView.Default, ImageAvatarCropView.From(image));
    }

    static async Task SeedImageAssetAsync(TestDbContextFactory dbFactory, string chatId, string imageId)
    {
        await using var dbContext = await dbFactory.CreateDbContextAsync();
        dbContext.ImageAssets.Add(new()
        {
            Id = imageId,
            ChatId = chatId,
            BlobName = $"images/{chatId}/{imageId}.png",
            StoredContentType = "image/png",
            StoredFileName = "image.png",
            OriginalContentType = "image/png",
            OriginalByteLength = PngBytes.Length,
            StoredByteLength = PngBytes.Length,
            Title = "Gemma",
            CreatedUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
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
