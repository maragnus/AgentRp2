using AgentRp.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public static class StoryImageEndpointExtensions
{
    public static IEndpointRouteBuilder MapStoryImageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/story-images/{imageId}", async (
            string imageId,
            IDbContextFactory<RpDbContext> dbContextFactory,
            IImageBlobStorage blobStorage,
            CancellationToken cancellationToken) =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var image = await dbContext.ImageAssets
                .AsNoTracking()
                .Where(x => x.Id == imageId)
                .Select(x => new { x.BlobName, x.StoredContentType, x.StoredFileName })
                .FirstOrDefaultAsync(cancellationToken);

            if (image is null)
                return Results.NotFound();

            var blob = await blobStorage.OpenReadAsync(image.BlobName, cancellationToken);
            return blob is null
                ? Results.NotFound()
                : Results.File(blob.Stream, image.StoredContentType, image.StoredFileName, enableRangeProcessing: true);
        });

        return endpoints;
    }
}
