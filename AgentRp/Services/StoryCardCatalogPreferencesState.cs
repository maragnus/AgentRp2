namespace AgentRp.Services;

public sealed record StoryCardCatalogPreferencesState(List<string> FavoriteTemplateIds)
{
    public static StoryCardCatalogPreferencesState Default { get; } = new([]);

    public static string SettingsKey(Guid userId) => $"story-card-catalog-preferences:{userId}";
}
