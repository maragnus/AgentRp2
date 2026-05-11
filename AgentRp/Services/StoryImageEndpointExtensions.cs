using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentRp.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using AgentRp.UserSystem;

namespace AgentRp.Services;

public static class StoryImageEndpointExtensions
{
	public static IEndpointRouteBuilder MapStoryImageEndpoints(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/story-images/{imageId}", (Func<string, ICurrentAppUserAccessor, IDbContextFactory<RpDbContext>, IAssetBlobStorage, CancellationToken, Task<IResult>>)async delegate(string imageId, ICurrentAppUserAccessor currentUserAccessor, IDbContextFactory<RpDbContext> dbContextFactory, IAssetBlobStorage blobStorage, CancellationToken cancellationToken)
		{
			CurrentAppUser user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
			await using (RpDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
			{
				var image = await (from x in dbContext.ImageAssets.AsNoTracking()
					where x.Id == imageId
					select new { x.UserId, x.BlobName, x.StoredContentType, x.StoredFileName }).FirstOrDefaultAsync(cancellationToken);
				if (image == null || (!user.IsAdmin && image.UserId != user.Id))
				{
					return Results.NotFound();
				}
				else
				{
					var blob = await blobStorage.OpenReadAsync(image.BlobName, cancellationToken);
					return blob == null ? Results.NotFound() : Results.File(blob.Stream, image.StoredContentType, image.StoredFileName, null, null, enableRangeProcessing: true);
				}
			}
		}).RequireAuthorization();
		return endpoints;
	}
}
