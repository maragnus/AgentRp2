using AgentRp.Models;
using AgentRp.Services;
using AgentRp.UserSystem;

namespace AgentRp.Session;

public sealed class UserImageLibrarySessionStore(
    IUserImageLibraryService imageLibrary,
    CurrentAppUser user,
    IEntityNotifier entityNotifier) : StoreBase
{
    public List<GalleryImage> Items { get; private set; } = [];

    public async Task LoadAsync()
    {
        Items = (await imageLibrary.LoadAsync(user)).ToList();
        await NotifyChangedAsync();
    }

    public Task RefreshAsync() => LoadAsync();

    public async Task AddGeneratedAsync(GalleryImage image)
    {
        Items.RemoveAll(existing => existing.Id == image.Id);
        Items.Insert(0, SessionCloner.Clone(image));
        await NotifyChangedAsync();
    }

    public async Task DeleteAsync(string imageId)
    {
        await imageLibrary.DeleteAsync(user, imageId);
        Items.RemoveAll(image => image.Id == imageId);
        await NotifyChangedAsync();
    }

    public async Task<ImageAvatarCropView> SetCropAsync(string imageId, ImageAvatarCropView crop)
    {
        var saved = await imageLibrary.SetCropAsync(user, imageId, crop);
        var image = Items.FirstOrDefault(image => image.Id == imageId);
        if (image is not null)
        {
            image.AvatarFocusXPercent = saved.FocusXPercent;
            image.AvatarFocusYPercent = saved.FocusYPercent;
            image.AvatarZoomPercent = saved.ZoomPercent;
        }

        await NotifyChangedAsync();
        await entityNotifier.PublishAsync(new("", "", EntityChangeKinds.ImageCrop, imageId));
        return saved;
    }

    public string NextGalleryImageId()
    {
        var index = Items
            .Select(image => image.Id)
            .Where(id => id.Length > 1 && id[0] == 'g' && int.TryParse(id[1..], out _))
            .Select(id => int.Parse(id[1..]))
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"g{index}";
    }
}
