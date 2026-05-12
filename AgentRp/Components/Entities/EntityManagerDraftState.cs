using AgentRp.Models;
using AgentRp.Session;
using AgentRp.Services;

namespace AgentRp.Components.Entities;

public sealed class EntityManagerDraftState
{
    public string Type { get; set; } = "characters";
    public string SelectedId { get; set; } = "";
    public RpCharacter? Character { get; set; }
    public RpLocation? Location { get; set; }
    public RpItem? Item { get; set; }
    public RpTimelineEntry? Timeline { get; set; }
    public NarratorProfileState? Narrator { get; set; }

    public static EntityManagerDraftState Empty(string type = "characters") => new()
    {
        Type = type
    };

    public static EntityManagerDraftState ForNarrator(NarratorProfileState profile) => new()
    {
        Type = "characters",
        SelectedId = EntityIds.Narrator,
        Narrator = SessionCloner.Clone(profile)
    };

    public static EntityManagerDraftState ForCharacter(RpCharacter character) => new()
    {
        Type = "characters",
        SelectedId = character.Id,
        Character = SessionCloner.Clone(character)
    };

    public static EntityManagerDraftState ForLocation(RpLocation location) => new()
    {
        Type = "locations",
        SelectedId = location.Id,
        Location = SessionCloner.Clone(location)
    };

    public static EntityManagerDraftState ForItem(RpItem item) => new()
    {
        Type = "items",
        SelectedId = item.Id,
        Item = SessionCloner.Clone(item)
    };

    public static EntityManagerDraftState ForTimeline(RpTimelineEntry entry) => new()
    {
        Type = "timeline",
        SelectedId = entry.Id,
        Timeline = SessionCloner.Clone(entry)
    };

    public EntityManagerDraftState Clone() => new()
    {
        Type = Type,
        SelectedId = SelectedId,
        Character = Character is null ? null : SessionCloner.Clone(Character),
        Location = Location is null ? null : SessionCloner.Clone(Location),
        Item = Item is null ? null : SessionCloner.Clone(Item),
        Timeline = Timeline is null ? null : SessionCloner.Clone(Timeline),
        Narrator = Narrator is null ? null : SessionCloner.Clone(Narrator)
    };
}
