namespace AgentRp.Services;

public sealed record StoryAssistantWorkflowDefinition(
    string Id,
    string PromptStageId,
    string Title,
    string Description,
    string Icon,
    string DisplayMessage);

public static class StoryAssistantWorkflowCatalog
{
    public const string PrepareStory = "prepare-story";
    public const string IntroduceCharacters = "introduce-characters";
    public const string IntroduceLocation = "introduce-location";
    public const string ChangeScene = "change-scene";

    static readonly IReadOnlyList<StoryAssistantWorkflowDefinition> WorkflowDefinitions =
    [
        new(
            PrepareStory,
            PromptLibraryStageIds.StoryAssistantPrepareStory,
            "Prepare a New Story",
            "Set direction, cast, places, items, and an opening scene.",
            "wand-sparkles",
            "Prepare a new story"),
        new(
            IntroduceCharacters,
            PromptLibraryStageIds.StoryAssistantIntroduceCharacters,
            "Introduce Characters",
            "Add new people with dynamics that push the story forward.",
            "users",
            "Introduce characters"),
        new(
            IntroduceLocation,
            PromptLibraryStageIds.StoryAssistantIntroduceLocation,
            "Introduce a Location",
            "Create a useful place and decide how it affects the current story.",
            "map-pin",
            "Introduce a location"),
        new(
            ChangeScene,
            PromptLibraryStageIds.StoryAssistantChangeScene,
            "Change the Scene",
            "Fast-forward, transition, or reset the scene with clear staging.",
            "route",
            "Change the scene")
    ];

    public static IReadOnlyList<StoryAssistantWorkflowDefinition> Workflows => WorkflowDefinitions;

    public static StoryAssistantWorkflowDefinition? Find(string id) =>
        WorkflowDefinitions.FirstOrDefault(workflow => string.Equals(workflow.Id, id, StringComparison.Ordinal));
}
