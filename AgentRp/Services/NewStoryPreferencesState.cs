namespace AgentRp.Services;

public sealed record NewStoryPreferencesState(bool EnableTts)
{
    public const string SettingsKey = "new-story-preferences";

    public static NewStoryPreferencesState Default { get; } = new(false);
}
