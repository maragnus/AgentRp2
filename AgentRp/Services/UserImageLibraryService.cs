using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Session;
using AgentRp.UserSystem;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public interface IUserImageLibraryService
{
    Task<IReadOnlyList<GalleryImage>> LoadAsync(CurrentAppUser user, CancellationToken cancellationToken = default);
    Task DeleteAsync(CurrentAppUser user, string imageId, CancellationToken cancellationToken = default);
    Task<ImageAvatarCropView> SetCropAsync(CurrentAppUser user, string imageId, ImageAvatarCropView crop, CancellationToken cancellationToken = default);
}

public sealed class UserImageLibraryService(
    IDbContextFactory<RpDbContext> dbContextFactory,
    IAssetBlobStorage? blobStorage = null,
    ILogger<UserImageLibraryService>? logger = null) : IUserImageLibraryService
{
    public async Task<IReadOnlyList<GalleryImage>> LoadAsync(CurrentAppUser user, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await AuthorizedRows(dbContext, user)
            .AsNoTracking()
            .OrderBy(row => row.SortOrder)
            .ThenByDescending(row => row.CreatedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(StoryEntityPersistenceMapper.ToModel).ToList();
    }

    public async Task DeleteAsync(CurrentAppUser user, string imageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageId))
            return;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await AuthorizedRows(dbContext, user)
            .Where(row => row.Id == imageId)
            .OrderBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return;

        dbContext.ImageAssets.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TryDeleteBlobAsync(row.BlobName, cancellationToken);
    }

    public async Task<ImageAvatarCropView> SetCropAsync(CurrentAppUser user, string imageId, ImageAvatarCropView crop, CancellationToken cancellationToken = default)
    {
        var normalized = ImageAvatarCropView.Normalize(crop.FocusXPercent, crop.FocusYPercent, crop.ZoomPercent);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await AuthorizedRows(dbContext, user)
            .Where(row => row.Id == imageId)
            .OrderBy(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is not null)
        {
            row.AvatarFocusXPercent = normalized.FocusXPercent;
            row.AvatarFocusYPercent = normalized.FocusYPercent;
            row.AvatarZoomPercent = normalized.ZoomPercent;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return normalized;
    }

    static IQueryable<ImageAssetRow> AuthorizedRows(RpDbContext dbContext, CurrentAppUser user) =>
        dbContext.ImageAssets.Where(row => row.UserId == user.Id);

    async Task TryDeleteBlobAsync(string blobName, CancellationToken cancellationToken)
    {
        if (blobStorage is null || string.IsNullOrWhiteSpace(blobName))
            return;

        try
        {
            await blobStorage.DeleteAsync(blobName, cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Deleting image library blob {BlobName} failed.", blobName);
        }
    }
}

public sealed class NullUserImageLibraryService : IUserImageLibraryService
{
    public static NullUserImageLibraryService Instance { get; } = new();

    public Task<IReadOnlyList<GalleryImage>> LoadAsync(CurrentAppUser user, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GalleryImage>>([]);

    public Task DeleteAsync(CurrentAppUser user, string imageId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<ImageAvatarCropView> SetCropAsync(CurrentAppUser user, string imageId, ImageAvatarCropView crop, CancellationToken cancellationToken = default) =>
        Task.FromResult(ImageAvatarCropView.Normalize(crop.FocusXPercent, crop.FocusYPercent, crop.ZoomPercent));
}
