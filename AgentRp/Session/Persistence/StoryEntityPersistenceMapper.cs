using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

internal static class StoryEntityPersistenceMapper
{
    public static RpCharacter ToModel(ChatCharacterRow row)
    {
        var model = PersistenceJson.Deserialize(row.ProfileJson, new RpCharacter());
        model.Id = row.Id;
        model.Name = row.Name;
        model.ImageId = row.ImageId;
        model.InScene = row.InScene;
        return model;
    }

    public static void Apply(RpCharacter character, ChatCharacterRow row, int sortOrder, DateTime now)
    {
        row.Id = character.Id;
        row.Name = character.Name;
        row.ImageId = character.ImageId;
        row.InScene = character.InScene;
        row.SortOrder = sortOrder;
        row.ProfileJson = PersistenceJson.Serialize(character);
        row.UpdatedUtc = now;
    }

    public static RpCharacterRelationship ToModel(ChatCharacterRelationshipRow row)
    {
        var model = PersistenceJson.Deserialize(row.DetailsJson, new RpCharacterRelationship());
        model.Id = row.Id;
        model.CharacterAId = row.CharacterAId;
        model.CharacterBId = row.CharacterBId;
        return model;
    }

    public static void Apply(RpCharacterRelationship relationship, ChatCharacterRelationshipRow row, int sortOrder, DateTime now)
    {
        row.Id = relationship.Id;
        row.CharacterAId = relationship.CharacterAId;
        row.CharacterBId = relationship.CharacterBId;
        row.SortOrder = sortOrder;
        row.DetailsJson = PersistenceJson.Serialize(relationship);
        row.UpdatedUtc = now;
    }

    public static RpLocation ToModel(ChatLocationRow row)
    {
        var model = PersistenceJson.Deserialize(row.DetailsJson, new RpLocation());
        model.Id = row.Id;
        model.Name = row.Name;
        model.ImageId = row.ImageId;
        model.IsActive = row.IsActive;
        return model;
    }

    public static void Apply(RpLocation location, ChatLocationRow row, int sortOrder, DateTime now)
    {
        row.Id = location.Id;
        row.Name = location.Name;
        row.ImageId = location.ImageId;
        row.IsActive = location.IsActive;
        row.SortOrder = sortOrder;
        row.DetailsJson = PersistenceJson.Serialize(location);
        row.UpdatedUtc = now;
    }

    public static RpItem ToModel(ChatItemRow row)
    {
        var model = PersistenceJson.Deserialize(row.DetailsJson, new RpItem());
        model.Id = row.Id;
        model.Name = row.Name;
        model.ImageId = row.ImageId;
        model.InScene = row.InScene;
        return model;
    }

    public static void Apply(RpItem item, ChatItemRow row, int sortOrder, DateTime now)
    {
        row.Id = item.Id;
        row.Name = item.Name;
        row.ImageId = item.ImageId;
        row.InScene = item.InScene;
        row.SortOrder = sortOrder;
        row.DetailsJson = PersistenceJson.Serialize(item);
        row.UpdatedUtc = now;
    }

    public static RpTimelineEntry ToModel(ChatTimelineEntryRow row)
    {
        var model = PersistenceJson.Deserialize(row.DetailsJson, new RpTimelineEntry());
        model.Id = row.Id;
        model.SnapshotId = row.SnapshotId;
        model.Title = row.Title;
        model.Date = row.DateText;
        return model;
    }

    public static void Apply(RpTimelineEntry entry, ChatTimelineEntryRow row, int sortOrder, DateTime now)
    {
        row.Id = entry.Id;
        row.SnapshotId = entry.SnapshotId;
        row.Title = entry.Title;
        row.DateText = entry.Date;
        row.SortOrder = sortOrder;
        row.DetailsJson = PersistenceJson.Serialize(entry);
        row.UpdatedUtc = now;
    }

    public static GalleryImage ToModel(ImageAssetRow row) => new()
    {
        Id = row.Id,
        Name = row.Title,
        Entity = row.Entity,
        EntityType = row.EntityType,
        Date = RelativeDateFormatter.FormatDate(row.CreatedUtc),
        Hue = row.Hue,
        Url = ImageGenerationService.BuildImageUrl(row.Id),
        AvatarFocusXPercent = row.AvatarFocusXPercent ?? 50,
        AvatarFocusYPercent = row.AvatarFocusYPercent ?? 50,
        AvatarZoomPercent = row.AvatarZoomPercent ?? 100
    };

    public static void Apply(GalleryImage image, ImageAssetRow row, int sortOrder)
    {
        row.Title = image.Name;
        row.Entity = image.Entity;
        row.EntityType = image.EntityType;
        row.Hue = image.Hue;
        row.SortOrder = sortOrder;
        row.AvatarFocusXPercent = image.AvatarFocusXPercent;
        row.AvatarFocusYPercent = image.AvatarFocusYPercent;
        row.AvatarZoomPercent = image.AvatarZoomPercent;
    }
}
