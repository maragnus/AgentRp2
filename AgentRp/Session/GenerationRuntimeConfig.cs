namespace AgentRp.Session;

public sealed record GenerationRuntimeConfig(
    ActiveModelSelectionsState ModelSelections,
    PromptLibraryState PromptLibrary,
    ModelTuningState ModelTuning)
{
    public static GenerationRuntimeConfig CreateDefault() =>
        new(ActiveModelSelectionsState.CreateDefault(), PromptLibraryState.CreateDefault(), ModelTuningState.CreateDefault());
}
