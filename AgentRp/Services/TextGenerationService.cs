using System.Text.Json;
using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Services;

public sealed record GenerateTurnRequest(
    string ParentTurnId,
    string Mode,
    string Guidance,
    string RequestedTurnShape,
    string RequestedActorCharacterId,
    string RequestedActorName);

public sealed record GenerateSnapshotRequest(string TurnId);

public sealed record GeneratedTurnResult(
    string ActorCharacterId,
    string ActorName,
    RpTurnPlan Plan,
    Dictionary<string, string> AppearanceByCharacterId,
    Dictionary<string, string> PrivateIntentByCharacterId,
    RpSceneFrame Scene,
    string Body,
    RpTurnTrace Trace);

public sealed record GeneratedSnapshotResult(
    string Summary,
    string EarlierPrivateIntentContinuity,
    Dictionary<string, string> CharacterAppearances,
    RpSceneFrame Scene,
    RpTurnTrace Trace);

public interface ITextGenerationService
{
    Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateTurnRequest request, CancellationToken cancellationToken = default);
    Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, GenerateSnapshotRequest request, CancellationToken cancellationToken = default);
}

public sealed class TranscriptGenerationException(string message, RpTurnTrace trace) : Exception(message)
{
    public RpTurnTrace Trace { get; } = trace;
}

public sealed class TextGenerationService(
    IResponseGenerationClient generationClient,
    IModelCapabilityCatalog capabilityCatalog,
    TranscriptPromptContextBuilder promptContextBuilder) : ITextGenerationService
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<GeneratedTurnResult> GenerateTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateTurnRequest request,
        CancellationToken cancellationToken = default)
    {
            ApplyCapabilities(providers);
            var selection = ResolveTextModel(providers);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ModelId = selection.Model.Id
        };
        try
        {
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                request.RequestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == request.RequestedActorCharacterId));
            var appearance = await RunAppearanceStepAsync(document, selection, context, trace, cancellationToken);
            var selectedActor = await RunSelectionStepAsync(document, selection, context, request, trace, cancellationToken);
            var plan = await RunPlanningStepAsync(document, selection, context, selectedActor, trace, cancellationToken);
            var prose = await RunProseStepAsync(document, selection, context, selectedActor, plan, trace, cancellationToken);
            FinalizeTrace(trace, "completed");

            var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(plan.PrivateIntent) && !string.IsNullOrWhiteSpace(selectedActor.Id))
                privateIntents[selectedActor.Id] = plan.PrivateIntent;

            var scene = SessionCloner.Clone(TranscriptGraph.GetActiveScene(document.Transcript));
            return new(
                selectedActor.Id,
                selectedActor.Name,
                new RpTurnPlan
                {
                    TurnShape = context.RequestedTurnShape,
                    Beat = plan.Beat,
                    Intent = plan.Intent,
                    ImmediateGoal = plan.ImmediateGoal,
                    WhyNow = plan.WhyNow,
                    ChangeIntroduced = plan.ChangeIntroduced,
                    Guardrails = plan.Guardrails
                },
                appearance,
                privateIntents,
                scene,
                prose,
                trace);
        }
        catch (Exception exception) when (exception is not TranscriptGenerationException)
        {
            FinalizeTrace(trace, "failed");
            trace.Data["error"] = exception.Message;
            throw new TranscriptGenerationException(exception.Message, trace);
        }
    }

    public async Task<GeneratedSnapshotResult> GenerateSnapshotAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        GenerateSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ModelId = selection.Model.Id
        };
        try
        {
            var context = promptContextBuilder.BuildSnapshotContext(document, request.TurnId);
            var tuning = ResolveTuning(document.ModelTuning, "snapshot");
            var tokens = promptContextBuilder.BuildTokens(context);
            var systemPrompt = RenderPrompt(document.PromptLibrary, "snapshot", "system", tokens);
            var userPrompt = RenderPrompt(document.PromptLibrary, "snapshot", "user", tokens);
            var startedUtc = DateTime.UtcNow;
            var completion = await SendStructuredAsync<SnapshotResponse>(selection, tuning, systemPrompt, userPrompt, "Generating snapshot", cancellationToken);
            var result = completion.Value;
            trace.Steps.Add(CreateStepTrace(
                "snapshot",
                "Snapshot",
                selection,
                startedUtc,
                DateTime.UtcNow,
                systemPrompt,
                userPrompt,
                completion,
                JsonSerializer.Serialize(result, JsonOptions),
                ""));
            FinalizeTrace(trace, "completed");
            var activePath = TranscriptGraph.GetActivePath(document.Transcript);
            var turnIndex = activePath.FindIndex(turn => turn.Id == request.TurnId);
            if (turnIndex >= 0)
                activePath = activePath.Take(turnIndex + 1).ToList();

            var latestTurn = activePath.LastOrDefault();
            return new(
                result.Summary,
                context.EarlierPrivateIntentContinuity,
                BuildSnapshotAppearances(activePath),
                SessionCloner.Clone(latestTurn?.Scene ?? document.Transcript.RootScene),
                trace);
        }
        catch (Exception exception) when (exception is not TranscriptGenerationException)
        {
            FinalizeTrace(trace, "failed");
            trace.Data["error"] = exception.Message;
            throw new TranscriptGenerationException(exception.Message, trace);
        }
    }

    async Task<Dictionary<string, string>> RunAppearanceStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "appearance");
        var tokens = promptContextBuilder.BuildTokens(context, "");
        var systemPrompt = RenderPrompt(document.PromptLibrary, "appearance", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "appearance", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStructuredAsync<AppearanceResponse>(selection, tuning, systemPrompt, userPrompt, "Generating appearance state", cancellationToken);
        var result = completion.Value;
        trace.Steps.Add(CreateStepTrace(
            "appearance",
            "Appearance",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            JsonSerializer.Serialize(result, JsonOptions),
            ""));
        return result.Characters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => ResolveCharacterId(document.Characters, pair.Key), pair => pair.Value, StringComparer.Ordinal);
    }

    async Task<(string Id, string Name)> RunSelectionStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        GenerateTurnRequest request,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RequestedActorCharacterId))
        {
            var explicitSelection = new SelectionResponse(request.RequestedActorName, "User override");
            var now = DateTime.UtcNow;
            trace.Steps.Add(CreateStepTrace(
                "selection",
                "Selection",
                selection,
                now,
                now,
                "User override",
                request.RequestedActorName,
                new("User override", 0, 0),
                JsonSerializer.Serialize(explicitSelection, JsonOptions),
                ""));
            return (request.RequestedActorCharacterId, request.RequestedActorName);
        }

        var tuning = ResolveTuning(document.ModelTuning, "selection");
        var tokens = promptContextBuilder.BuildTokens(context, "");
        var systemPrompt = RenderPrompt(document.PromptLibrary, "selection", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "selection", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStructuredAsync<SelectionResponse>(selection, tuning, systemPrompt, userPrompt, "Selecting transcript actor", cancellationToken);
        var result = completion.Value;
        trace.Steps.Add(CreateStepTrace(
            "selection",
            "Selection",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            JsonSerializer.Serialize(result, JsonOptions),
            ""));
        var actorId = ResolveCharacterId(document.Characters, result.CharacterName);
        var actorName = document.Characters.FirstOrDefault(character => character.Id == actorId)?.Name ?? result.CharacterName;
        return (actorId, actorName);
    }

    async Task<PlanningResponse> RunPlanningStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        (string Id, string Name) actor,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "planning");
        var tokens = promptContextBuilder.BuildTokens(context with { Actor = document.Characters.FirstOrDefault(character => character.Id == actor.Id) }, "");
        var systemPrompt = RenderPrompt(document.PromptLibrary, "planning", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "planning", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStructuredAsync<PlanningResponse>(selection, tuning, systemPrompt, userPrompt, "Planning transcript turn", cancellationToken);
        var result = completion.Value;
        trace.Steps.Add(CreateStepTrace(
            "planning",
            "Planning",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            JsonSerializer.Serialize(result, JsonOptions),
            ""));
        return result;
    }

    async Task<string> RunProseStepAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        TurnPromptContext context,
        (string Id, string Name) actor,
        PlanningResponse plan,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "prose");
        var planningOutput = $"Beat: {plan.Beat}\nIntent: {plan.Intent}\nImmediate Goal: {plan.ImmediateGoal}\nWhy Now: {plan.WhyNow}\nChange Introduced: {plan.ChangeIntroduced}\nGuardrails: {plan.Guardrails}";
        var tokens = promptContextBuilder.BuildTokens(context with { Actor = document.Characters.FirstOrDefault(character => character.Id == actor.Id) }, planningOutput);
        var systemPrompt = RenderPrompt(document.PromptLibrary, "prose", "system", tokens);
        var userPrompt = RenderPrompt(document.PromptLibrary, "prose", "user", tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStreamingTextAsync(selection, tuning, systemPrompt, userPrompt, "Writing transcript prose", cancellationToken);
        trace.Steps.Add(CreateStepTrace(
            "prose",
            "Prose",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            "",
            ""));
        return completion.Text.Trim();
    }

    static string RenderPrompt(PromptLibraryState library, string stepId, string field, IReadOnlyDictionary<string, string> tokens)
    {
        var defaults = PromptLibraryState.CreateDefault();
        var pair = library.Prompts.TryGetValue(stepId, out var configured)
            ? configured
            : defaults.Prompts[stepId];
        var template = field == "system" ? pair.System : pair.User;
        return PromptTemplateRenderer.Render(template, tokens);
    }

    static ModelTuningStepState ResolveTuning(ModelTuningState state, string stepId)
    {
        if (state.Values.TryGetValue(stepId, out var tuning))
            return tuning;

        var defaults = ModelTuningState.CreateDefault();
        return defaults.Values.TryGetValue(stepId, out var defaultTuning) ? defaultTuning : new();
    }

    async Task<StructuredTextCompletion<T>> SendStructuredAsync<T>(
        ActiveTextModel selection,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        string operationName,
        CancellationToken cancellationToken)
    {
        var completion = await generationClient.GetResponseAsync<T>(new(
            selection.Provider,
            selection.Model,
            selection.Capabilities,
            tuning,
            systemPrompt,
            userPrompt,
            operationName), cancellationToken);
        return new(completion.Value, completion.Text, completion.InputTokens, completion.OutputTokens);
    }

    async Task<TextCompletion> SendStreamingTextAsync(
        ActiveTextModel selection,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        string operationName,
        CancellationToken cancellationToken)
    {
        var text = new System.Text.StringBuilder();
        var inputTokens = 0;
        var outputTokens = 0;
        await foreach (var update in generationClient.GetStreamingResponseAsync(new(
            selection.Provider,
            selection.Model,
            selection.Capabilities,
            tuning,
            systemPrompt,
            userPrompt,
            operationName), cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.TextDelta))
                text.Append(update.TextDelta);
            if (update.Completed)
            {
                inputTokens = update.InputTokens;
                outputTokens = update.OutputTokens;
            }
        }

        return new(text.ToString(), inputTokens, outputTokens);
    }

    static RpTurnTraceStep CreateStepTrace(
        string id,
        string label,
        ActiveTextModel selection,
        DateTime startedUtc,
        DateTime completedUtc,
        string systemPrompt,
        string userPrompt,
        TextCompletion completion,
        string structuredOutputJson,
        string error) => new()
    {
        Id = id,
        Label = label,
        Status = string.IsNullOrWhiteSpace(error) ? "completed" : "failed",
        StartedUtc = startedUtc,
        CompletedUtc = completedUtc,
        ProviderId = selection.Provider.Id,
        ProviderName = selection.Provider.Name,
        ModelId = selection.Model.Id,
        InputTokens = completion.InputTokens,
        OutputTokens = completion.OutputTokens,
        TotalTokens = completion.InputTokens + completion.OutputTokens,
        DurationSeconds = (completedUtc - startedUtc).TotalSeconds,
        SystemPrompt = systemPrompt,
        UserPrompt = userPrompt,
        RawOutput = completion.Text,
        StructuredOutputJson = structuredOutputJson,
        Error = error
    };

    static void FinalizeTrace(RpTurnTrace trace, string status)
    {
        trace.Status = status;
        trace.CompletedUtc = DateTime.UtcNow;
        trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
        trace.InputTokens = trace.Steps.Sum(step => step.InputTokens);
        trace.OutputTokens = trace.Steps.Sum(step => step.OutputTokens);
        trace.TotalTokens = trace.Steps.Sum(step => step.TotalTokens);
        var actor = trace.Data["actorName"]?.GetValue<string>() ?? "Narrator";
        trace.Summary = $"{status[..1].ToUpperInvariant()}{status[1..]} · {actor} · {string.Join(" -> ", trace.Steps.Select(step => step.Label))}";
    }

    void ApplyCapabilities(IReadOnlyList<AiProvider> providers)
    {
        foreach (var provider in providers)
            capabilityCatalog.ApplyResolvedCapabilities(provider);
    }

    static ActiveTextModel ResolveTextModel(IReadOnlyList<AiProvider> providers) =>
        TextModelTuningCatalog.TryResolveActiveTextModel(providers)
        ?? throw new InvalidOperationException("Generating transcript text failed because no text-capable model is enabled.");

    static string ResolveCharacterId(IEnumerable<RpCharacter> characters, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var match = characters.FirstOrDefault(character => string.Equals(character.Name, name, StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? "";
    }

    static Dictionary<string, string> BuildSnapshotAppearances(IEnumerable<RpTranscriptTurn> turns)
    {
        var appearances = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var turn in turns)
        {
            foreach (var pair in turn.AppearanceByCharacterId)
                appearances[pair.Key] = pair.Value;
        }

        return appearances;
    }

    record TextCompletion(string Text, int InputTokens, int OutputTokens);
    sealed record StructuredTextCompletion<T>(T Value, string Text, int InputTokens, int OutputTokens) : TextCompletion(Text, InputTokens, OutputTokens);
    sealed class AppearanceResponse
    {
        public Dictionary<string, string> Characters { get; set; } = [];
    }

    sealed class SelectionResponse
    {
        public SelectionResponse()
        {
        }

        public SelectionResponse(string characterName, string reason)
        {
            CharacterName = characterName;
            Reason = reason;
        }

        public string CharacterName { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    sealed class PlanningResponse
    {
        public string Beat { get; set; } = "";
        public string Intent { get; set; } = "";
        public string ImmediateGoal { get; set; } = "";
        public string WhyNow { get; set; } = "";
        public string ChangeIntroduced { get; set; } = "";
        public string Guardrails { get; set; } = "";
        public string PrivateIntent { get; set; } = "";
    }

    sealed class SnapshotResponse
    {
        public string Summary { get; set; } = "";
    }
}
