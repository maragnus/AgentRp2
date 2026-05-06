using System.Text.Json;
using AgentRp.Models;
using AgentRp.Serialization;
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
    List<RpTranscriptSnapshotFact> Facts,
    List<RpTranscriptSnapshotTimelineEntry> TimelineEntries,
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
    IModelGenerationClient generationClient,
    IModelCapabilityCatalog capabilityCatalog,
    TranscriptPromptContextBuilder promptContextBuilder,
    PromptLibraryService? promptLibraryService = null) : ITextGenerationService
{
    readonly PromptLibraryService _promptLibraryService = promptLibraryService ?? new();

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
            if (!selection.Capabilities.CanGenerateStructuredText)
                return await GenerateDumbProseTurnAsync(document, selection, request, context, trace, cancellationToken);

            var appearance = await RunAppearanceStepAsync(document, selection, context, trace, cancellationToken);
            var selectedActor = await RunSelectionStepAsync(document, selection, context, request, trace, cancellationToken);
            var selectedContext = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                request.RequestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == selectedActor.Id));
            var plan = await RunPlanningStepAsync(document, selection, selectedContext, selectedActor, trace, cancellationToken);
            var prose = await RunProseStepAsync(document, selection, selectedContext, selectedActor, plan, trace, cancellationToken);
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
                    TurnShape = ResolveTurnShape(plan.TurnShape, context.RequestedTurnShape),
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
            if (!selection.Capabilities.CanGenerateStructuredText)
                throw new InvalidOperationException("Creating a snapshot failed because the active model has structured output disabled.");

            var context = promptContextBuilder.BuildSnapshotContext(document, request.TurnId);
            var tuning = ResolveTuning(document.ModelTuning, "snapshot");
            var tokens = promptContextBuilder.BuildTokens(context);
            var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Snapshot, tokens);
            var startedUtc = DateTime.UtcNow;
            var completion = await SendStructuredAsync<SnapshotResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Generating snapshot", cancellationToken);
            var result = completion.Value;
            trace.Steps.Add(CreateStepTrace(
                "snapshot",
                "Snapshot",
                selection,
                startedUtc,
                DateTime.UtcNow,
                prompt.SystemPrompt,
                prompt.UserPrompt,
                completion,
                JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
                ""));
            FinalizeTrace(trace, "completed");
            var activePath = TranscriptGraph.GetActivePath(document.Transcript);
            var turnIndex = activePath.FindIndex(turn => turn.Id == request.TurnId);
            if (turnIndex >= 0)
                activePath = activePath.Take(turnIndex + 1).ToList();

            var latestTurn = activePath.LastOrDefault();
            return new(
                ResolveSnapshotSummary(result),
                context.EarlierPrivateIntentContinuity,
                NormalizeSnapshotFacts(result.Facts),
                NormalizeSnapshotTimelineEntries(result.TimelineEntries),
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

    async Task<GeneratedTurnResult> GenerateDumbProseTurnAsync(
        RpChatDocument document,
        ActiveTextModel selection,
        GenerateTurnRequest request,
        TurnPromptContext context,
        RpTurnTrace trace,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestedActorCharacterId))
            throw new InvalidOperationException("Generating transcript prose failed because Respond As is required when the active model has structured output disabled.");

        var actor = (request.RequestedActorCharacterId, request.RequestedActorName);
        var plan = CreateDumbProsePlan(context, request);
        var prose = await RunProseStepAsync(document, selection, context, actor, plan, trace, cancellationToken);
        FinalizeTrace(trace, "completed");

        var scene = SessionCloner.Clone(TranscriptGraph.GetActiveScene(document.Transcript));
        return new(
            request.RequestedActorCharacterId,
            request.RequestedActorName,
            new RpTurnPlan
            {
                TurnShape = ResolveTurnShape(plan.TurnShape, context.RequestedTurnShape),
                Beat = plan.Beat,
                Intent = plan.Intent,
                ImmediateGoal = plan.ImmediateGoal,
                WhyNow = plan.WhyNow,
                ChangeIntroduced = plan.ChangeIntroduced,
                Guardrails = plan.Guardrails
            },
            [],
            [],
            scene,
            prose,
            trace);
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
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Appearance, tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStructuredAsync<AppearanceResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Generating appearance state", cancellationToken);
        var result = completion.Value;
        trace.Steps.Add(CreateStepTrace(
            "appearance",
            "Appearance",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
            ""));
        return result.Characters
            ?.Where(character => !string.IsNullOrWhiteSpace(character.CharacterName))
            .Select(character => ResolveAppearanceCharacter(document.Characters, character))
            .Where(pair => pair is not null && !string.IsNullOrWhiteSpace(pair.Value.Appearance))
            .GroupBy(pair => pair!.Value.CharacterId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last()!.Value.Appearance, StringComparer.Ordinal)
        ?? new(StringComparer.Ordinal);
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
                new ModelTextCompletion("User override", 0, 0, ""),
                JsonSerializer.Serialize(explicitSelection, AppJsonSerializerOptions.IndentedWeb),
                ""));
            return (request.RequestedActorCharacterId, request.RequestedActorName);
        }

        var tuning = ResolveTuning(document.ModelTuning, "selection");
        var tokens = promptContextBuilder.BuildTokens(context, "");
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Selection, tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStructuredAsync<SelectionResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Selecting transcript actor", cancellationToken);
        var result = completion.Value;
        trace.Steps.Add(CreateStepTrace(
            "selection",
            "Selection",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
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
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Planning, tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStructuredAsync<PlanningResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Planning transcript turn", cancellationToken);
        var result = completion.Value;
        trace.Steps.Add(CreateStepTrace(
            "planning",
            "Planning",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
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
        var turnShape = ResolveTurnShape(plan.TurnShape, context.RequestedTurnShape);
        var planningOutput = BuildPlanningOutput(plan, turnShape);
        var tokens = promptContextBuilder.BuildProseTokens(
            context with { Actor = document.Characters.FirstOrDefault(character => character.Id == actor.Id) },
            planningOutput,
            turnShape,
            plan.Beat,
            plan.Intent,
            plan.ImmediateGoal,
            plan.WhyNow,
            plan.ChangeIntroduced,
            plan.PrivateIntent,
            plan.Guardrails,
            document.PromptLibrary);
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Prose, tokens);
        var startedUtc = DateTime.UtcNow;
        var completion = await SendStreamingTextAsync(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Writing transcript prose", cancellationToken);
        trace.Steps.Add(CreateStepTrace(
            "prose",
            "Prose",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            "",
            ""));
        return completion.Text.Trim();
    }

    static PlanningResponse CreateDumbProsePlan(TurnPromptContext context, GenerateTurnRequest request)
    {
        var guidance = string.IsNullOrWhiteSpace(request.Guidance)
            ? "Continue the scene in character."
            : request.Guidance.Trim();
        return new()
        {
            TurnShape = context.RequestedTurnShape,
            Beat = guidance,
            Intent = "Write the next prose turn.",
            ImmediateGoal = "Continue the active exchange.",
            WhyNow = "The user requested the next turn.",
            ChangeIntroduced = "Continue the scene without structured planning.",
            Guardrails = $"Turn shape: {context.RequestedTurnShape}. Stay grounded in the visible scene."
        };
    }

    static ModelTuningStepState ResolveTuning(ModelTuningState state, string stepId)
    {
        if (state.Values.TryGetValue(stepId, out var tuning))
            return tuning;

        var defaults = ModelTuningState.CreateDefault();
        return defaults.Values.TryGetValue(stepId, out var defaultTuning) ? defaultTuning : new();
    }

    async Task<ModelStructuredCompletion<T>> SendStructuredAsync<T>(
        ActiveTextModel selection,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        string operationName,
        CancellationToken cancellationToken)
    {
        return await generationClient.GenerateStructuredAsync<T>(new(
            selection.Provider,
            selection.Model,
            selection.Capabilities,
            tuning,
            systemPrompt,
            userPrompt,
            operationName), cancellationToken);
    }

    async Task<ModelTextCompletion> SendStreamingTextAsync(
        ActiveTextModel selection,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        string operationName,
        CancellationToken cancellationToken)
    {
        return await generationClient.GenerateStreamingTextAsync(new(
            selection.Provider,
            selection.Model,
            selection.Capabilities,
            tuning,
            systemPrompt,
            userPrompt,
            operationName), cancellationToken);
    }

    static RpTurnTraceStep CreateStepTrace(
        string id,
        string label,
        ActiveTextModel selection,
        DateTime startedUtc,
        DateTime completedUtc,
        string systemPrompt,
        string userPrompt,
        ModelTextCompletion completion,
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

    static (string CharacterId, string Appearance)? ResolveAppearanceCharacter(
        IEnumerable<RpCharacter> characters,
        AppearanceCharacterResponse response)
    {
        var characterId = ResolveCharacterId(characters, response.CharacterName);
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        var currentAppearance = response.HasCurrentSceneState
            ? response.CurrentAppearance?.Trim() ?? ""
            : "";
        return (characterId, currentAppearance);
    }

    static string BuildPlanningOutput(PlanningResponse plan, string turnShape) =>
        $"""
        Turn Shape: {turnShape}
        Beat: {plan.Beat}
        Intent: {plan.Intent}
        Immediate Goal: {plan.ImmediateGoal}
        Why Now: {plan.WhyNow}
        Change Introduced: {plan.ChangeIntroduced}
        Private Intent: {plan.PrivateIntent}
        Guardrails: {plan.Guardrails}
        """;

    static string ResolveTurnShape(string requestedByPlanner, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(requestedByPlanner) ? fallback : requestedByPlanner;
        return string.IsNullOrWhiteSpace(value) ? "Brief" : value.Trim();
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

    public sealed record AppearanceResponse(
        string? Summary,
        IReadOnlyList<AppearanceCharacterResponse>? Characters);

    public sealed record AppearanceCharacterResponse(
        string CharacterName,
        bool HasCurrentSceneState,
        string? CurrentAppearance);

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
        public string TurnShape { get; set; } = "";
        public string Beat { get; set; } = "";
        public string Intent { get; set; } = "";
        public string ImmediateGoal { get; set; } = "";
        public string WhyNow { get; set; } = "";
        public string ChangeIntroduced { get; set; } = "";
        public string Guardrails { get; set; } = "";
        public string PrivateIntent { get; set; } = "";
    }

    static string ResolveSnapshotSummary(SnapshotResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.NarrativeSummary))
            return response.NarrativeSummary.Trim();

        return response.Summary.Trim();
    }

    static List<RpTranscriptSnapshotFact> NormalizeSnapshotFacts(IReadOnlyList<SnapshotFactResponse>? facts) =>
        facts?
            .Where(fact => !string.IsNullOrWhiteSpace(fact.Title))
            .Select(fact => new RpTranscriptSnapshotFact
            {
                Title = fact.Title.Trim(),
                Summary = fact.Summary.Trim(),
                Details = fact.Details.Trim(),
                CharacterNames = NormalizeNames(fact.CharacterNames),
                LocationNames = NormalizeNames(fact.LocationNames),
                ItemNames = NormalizeNames(fact.ItemNames)
            })
            .ToList()
        ?? [];

    static List<RpTranscriptSnapshotTimelineEntry> NormalizeSnapshotTimelineEntries(IReadOnlyList<SnapshotTimelineEntryResponse>? entries) =>
        entries?
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
            .Select(entry => new RpTranscriptSnapshotTimelineEntry
            {
                WhenText = entry.WhenText.Trim(),
                Title = entry.Title.Trim(),
                Summary = entry.Summary.Trim(),
                Details = entry.Details.Trim(),
                CharacterNames = NormalizeNames(entry.CharacterNames),
                LocationNames = NormalizeNames(entry.LocationNames),
                ItemNames = NormalizeNames(entry.ItemNames)
            })
            .ToList()
        ?? [];

    static List<string> NormalizeNames(IReadOnlyList<string>? names) =>
        names?.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

    public sealed class SnapshotFactResponse
    {
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public IReadOnlyList<string>? CharacterNames { get; set; }
        public IReadOnlyList<string>? LocationNames { get; set; }
        public IReadOnlyList<string>? ItemNames { get; set; }
    }

    public sealed class SnapshotTimelineEntryResponse
    {
        public string WhenText { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Details { get; set; } = "";
        public IReadOnlyList<string>? CharacterNames { get; set; }
        public IReadOnlyList<string>? LocationNames { get; set; }
        public IReadOnlyList<string>? ItemNames { get; set; }
    }

    sealed class SnapshotResponse
    {
        public string NarrativeSummary { get; set; } = "";
        public string Summary { get; set; } = "";
        public IReadOnlyList<SnapshotFactResponse>? Facts { get; set; }
        public IReadOnlyList<SnapshotTimelineEntryResponse>? TimelineEntries { get; set; }
    }
}
