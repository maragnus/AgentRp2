using System;
using System.IO;
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

public static class StoryAudioEndpointExtensions
{
	private sealed record StoryAudioEndpointRow(Guid UserId, string Status, string BlobName, long StoredByteLength, string ContentType, string FileName, string ErrorMessage, DateTime CreatedUtc, DateTime? CompletedUtc);

	public static IEndpointRouteBuilder MapStoryAudioEndpoints(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/story-audio/{voiceMessageId}", (Func<string, HttpContext, ICurrentAppUserAccessor, IDbContextFactory<RpDbContext>, IAssetBlobStorage, IVoiceMessageStreamCoordinator, CancellationToken, Task<IResult>>)async delegate(string voiceMessageId, HttpContext context, ICurrentAppUserAccessor currentUserAccessor, IDbContextFactory<RpDbContext> dbContextFactory, IAssetBlobStorage blobStorage, IVoiceMessageStreamCoordinator streamCoordinator, CancellationToken cancellationToken)
		{
			CurrentAppUser user = await currentUserAccessor.GetCurrentUserAsync(cancellationToken);
			IResult result;
			await using (RpDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
			{
				StoryAudioEndpointRow audio = await (from x in dbContext.SpeechAssets.AsNoTracking()
					where x.Id == voiceMessageId
					orderby x.Id
					select new StoryAudioEndpointRow(x.UserId, x.Status, x.BlobName, x.StoredByteLength, x.ContentType, x.FileName, x.ErrorMessage, x.CreatedUtc, x.CompletedUtc)).FirstOrDefaultAsync(cancellationToken);
				if ((object)audio == null)
				{
					result = Results.NotFound();
				}
				else if (!user.IsAdmin && audio.UserId != user.Id)
				{
					result = Results.NotFound();
				}
				else if (audio.Status == "Ready")
				{
					result = await ServeReadyAudioAsync(voiceMessageId, context, blobStorage, audio, cancellationToken);
				}
				else if (audio.Status == "Failed")
				{
					result = Results.Problem(string.IsNullOrWhiteSpace(audio.ErrorMessage) ? "Reading aloud failed while generating the audio." : audio.ErrorMessage, null, 500, "Reading aloud failed");
				}
				else
				{
					VoiceMessageStartResult start = await streamCoordinator.EnsureStartedAsync(voiceMessageId, cancellationToken);
					if (!start.Started)
					{
						result = Results.Problem(start.ErrorMessage, null, 500, "Reading aloud failed");
					}
					else
					{
						context.Response.Headers.CacheControl = "no-store";
						result = Results.Stream((Stream stream) => streamCoordinator.CopyLiveAsync(voiceMessageId, stream, context.RequestAborted), ResolveContentType(audio.ContentType), audio.FileName);
					}
				}
			}
			return result;
		}).RequireAuthorization();
		return endpoints;
	}

	private static async Task<IResult> ServeReadyAudioAsync(string voiceMessageId, HttpContext context, IAssetBlobStorage blobStorage, StoryAudioEndpointRow audio, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(audio.BlobName) || audio.StoredByteLength <= 0)
		{
			return Results.Problem("Reading aloud failed because the stored audio is missing.", null, 500, "Reading aloud failed");
		}
		StoredAssetBlob blob = await blobStorage.OpenReadAsync(audio.BlobName, cancellationToken);
		if ((object)blob == null)
		{
			return Results.NotFound();
		}
		context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
		context.Response.Headers.ETag = $"\"{voiceMessageId}-{audio.StoredByteLength}-{(audio.CompletedUtc ?? audio.CreatedUtc).Ticks}\"";
		return Results.File(blob.Stream, ResolveContentType(audio.ContentType, blob.ContentType), audio.FileName, null, null, enableRangeProcessing: true);
	}

	private static string ResolveContentType(string contentType, string fallback = "audio/mpeg")
	{
		return string.IsNullOrWhiteSpace(contentType) ? fallback : contentType;
	}
}
