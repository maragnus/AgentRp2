using AgentRp.Models;

namespace AgentRp.Session;

public static class StoryEntityTimestamps
{
    public static void Touch(RpCharacter entity, DateTime timestamp) => entity.UpdatedUtc = timestamp;
    public static void Touch(RpCharacterRelationship entity, DateTime timestamp) => entity.UpdatedUtc = timestamp;
    public static void Touch(RpLocation entity, DateTime timestamp) => entity.UpdatedUtc = timestamp;
    public static void Touch(RpItem entity, DateTime timestamp) => entity.UpdatedUtc = timestamp;
    public static void Touch(RpTimelineEntry entity, DateTime timestamp) => entity.UpdatedUtc = timestamp;
    public static void Touch(ChatDirectionState state, DateTime timestamp) => state.UpdatedUtc = timestamp;
    public static void Touch(CharacterTraitLibraryState state, DateTime timestamp) => state.UpdatedUtc = timestamp;

    public static DateTime LatestStoryEntityUpdateUtc(RpChatDocument document) =>
        new[]
        {
            document.Characters.Select(item => item.UpdatedUtc).DefaultIfEmpty().Max(),
            document.CharacterRelationships.Select(item => item.UpdatedUtc).DefaultIfEmpty().Max(),
            document.Locations.Select(item => item.UpdatedUtc).DefaultIfEmpty().Max(),
            document.Items.Select(item => item.UpdatedUtc).DefaultIfEmpty().Max(),
            document.Timeline.Select(item => item.UpdatedUtc).DefaultIfEmpty().Max(),
            document.ChatDirection.UpdatedUtc
        }.Max();
}
