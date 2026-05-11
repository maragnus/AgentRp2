using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

internal static class ChatPersistenceMapper
{
    public static RpChat ToModel(RpChatRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        Title = row.Title,
        Updated = row.Updated,
        LastMessageUtc = row.LastMessageUtc,
        LastGeneratedTurnNumber = row.LastGeneratedTurnNumber,
        Starred = row.Starred,
        Messages = row.ActiveTurnCount > 0 ? row.ActiveTurnCount : row.Messages,
        Location = string.IsNullOrWhiteSpace(row.ActiveLocationName) ? row.Location : row.ActiveLocationName,
        ActiveLocation = string.IsNullOrWhiteSpace(row.ActiveLocationName)
            ? null
            : new() { Id = row.ActiveLocationId, Name = row.ActiveLocationName }
    };

    public static StoryPreview ToPreview(RpChatRow row, StoryPreviewLocation? location, List<StoryPreviewCharacter> characters) => new()
    {
        ChatId = row.Id,
        Title = row.Title,
        Starred = row.Starred,
        VisibleTurnCount = row.ActiveTurnCount > 0 ? row.ActiveTurnCount : row.Messages,
        LastGeneratedTurnNumber = row.LastGeneratedTurnNumber,
        LastMessageUtc = row.LastMessageUtc,
        Updated = row.Updated,
        ActiveLocation = location ?? FallbackLocation(row),
        SceneCharacters = characters
    };

    public static void Apply(RpChat chat, RpChatRow row, int sortOrder, DateTime now)
    {
        row.Title = chat.Title;
        row.UserId = chat.UserId;
        row.Updated = chat.Updated;
        row.LastMessageUtc = chat.LastMessageUtc;
        row.LastGeneratedTurnNumber = chat.LastGeneratedTurnNumber;
        row.Starred = chat.Starred;
        row.Messages = chat.Messages;
        row.Location = chat.Location;
        row.SortOrder = sortOrder;
        row.UpdatedUtc = now;
    }

    public static void ApplyTranscriptPreview(RpChatDocument document, RpChatRow row)
    {
        var scene = TranscriptGraph.GetVisibleScene(document.Transcript);
        var location = document.Locations.FirstOrDefault(location => location.Id == scene.LocationId);
        row.ActiveLeafTurnId = document.Transcript.ActiveLeafTurnId;
        row.ActiveTurnCount = document.Chat.Messages;
        row.ActiveLocationId = scene.LocationId;
        row.ActiveLocationName = location?.Name
            ?? (string.IsNullOrWhiteSpace(scene.LocationName) ? document.Chat.Location : scene.LocationName);
        row.SnapshotCount = document.Transcript.Snapshots.Count;
    }

    public static StoryPreviewAvatar? ToPreviewAvatar(ImageAssetRow? image)
    {
        if (image is null)
            return null;

        return new()
        {
            ImageId = image.Id,
            Url = ImageGenerationService.BuildImageUrl(image.Id),
            FocusXPercent = image.AvatarFocusXPercent ?? 50,
            FocusYPercent = image.AvatarFocusYPercent ?? 50,
            ZoomPercent = image.AvatarZoomPercent ?? 100
        };
    }

    static StoryPreviewLocation? FallbackLocation(RpChatRow row) =>
        string.IsNullOrWhiteSpace(row.ActiveLocationName) && string.IsNullOrWhiteSpace(row.Location)
            ? null
            : new()
            {
                LocationId = row.ActiveLocationId,
                Name = string.IsNullOrWhiteSpace(row.ActiveLocationName) ? row.Location : row.ActiveLocationName
            };
}
