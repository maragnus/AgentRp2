using System.Text;
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
    string RequestedActorName,
    bool RequestedNarrator = false,
    RpSceneFrame? SceneOverride = null);

public sealed record GenerateProseFromPlanRequest(
    string ParentTurnId,
    string Mode,
    string Guidance,
    string ActorCharacterId,
    string ActorName,
    bool RequestedNarrator,
    RpTurnPlan Plan,
    IReadOnlyDictionary<string, string> AppearanceByCharacterId,
    IReadOnlyDictionary<string, string> PrivateIntentByCharacterId,
    RpSceneFrame Scene);

public sealed record GeneratePlanAndProseRequest(
    string ParentTurnId,
    string Mode,
    string Guidance,
    string RequestedTurnShape,
    string ActorCharacterId,
    string ActorName,
    bool RequestedNarrator,
    IReadOnlyDictionary<string, string> AppearanceByCharacterId,
    RpSceneFrame Scene);

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
    List<RpTranscriptSnapshotTimelineEntry> TimelineEntries,
    RpTurnTrace Trace);

public sealed record TranscriptProseUpdate(
    string ParentTurnId,
    string Mode,
    string Guidance,
    string ActorCharacterId,
    string ActorName,
    RpTurnPlan Plan,
    RpSceneFrame Scene,
    string Body);

public interface ITextGenerationService
{
    Task<GeneratedTurnResult> GenerateTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default);
    Task<GeneratedTurnResult> GeneratePlanAndProseAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GeneratePlanAndProseRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default);
    Task<GeneratedTurnResult> GenerateProseFromPlanAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateProseFromPlanRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default);
    Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateSnapshotRequest request, CancellationToken cancellationToken = default);
}

public sealed record TranscriptGenerationProgress(Func<RpTurnTrace, Task> OnChanged, Func<TranscriptProseUpdate, Task>? OnProseChanged = null)
{
    public Task ReportAsync(RpTurnTrace trace) => OnChanged(trace);
    public Task ReportProseAsync(TranscriptProseUpdate update) => OnProseChanged?.Invoke(update) ?? Task.CompletedTask;
}

public sealed class TranscriptGenerationException(string message, RpTurnTrace trace) : Exception(message)
{
    public RpTurnTrace Trace { get; } = trace;
}

public sealed class TextGenerationService(
    IModelGenerationClient generationClient,
    IModelCapabilityCatalog capabilityCatalog,
    TranscriptPromptContextBuilder promptContextBuilder,
    PromptLibraryService? promptLibraryService = null,
    IAudioTagGuideService? audioTagGuideService = null) : ITextGenerationService
{
    readonly PromptLibraryService _promptLibraryService = promptLibraryService ?? new();
    readonly IAudioTagGuideService _audioTagGuideService = audioTagGuideService ?? new AudioTagGuideService();

    public async Task<GeneratedTurnResult> GenerateTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateTurnRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers, modelSelections);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ProviderType = selection.Provider.Type,
            ModelId = selection.Model.Id
        };
        try
        {
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                request.RequestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == request.RequestedActorCharacterId),
                request.RequestedNarrator,
                request.SceneOverride);
            var useSelectionStep = NeedsSelectionStep(request);
            SeedTurnSteps(trace, selection, selection.Capabilities.CanGenerateStructuredText, useSelectionStep);
            await ReportProgressAsync(progress, trace);
            if (!selection.Capabilities.CanGenerateStructuredText)
                return await GenerateDumbProseTurnAsync(document, providers, modelSelections, selection, request, context, trace, progress, cancellationToken);

            var appearance = await RunAppearanceStepAsync(document, selection, context, trace, progress, cancellationToken);
            var selectedActor = useSelectionStep
                ? await RunSelectionStepAsync(document, selection, context, trace, progress, cancellationToken)
                : ResolveRequestedActor(request);
            var selectedContext = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                request.RequestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == selectedActor.Id),
                request.RequestedNarrator,
                request.SceneOverride,
                appearance);
            var plan = await RunPlanningStepAsync(document, selection, selectedContext, selectedActor, trace, progress, cancellationToken);
            var prose = await RunProseStepAsync(document, providers, modelSelections, selection, request, selectedContext, selectedActor, plan, trace, progress, cancellationToken, progressSceneOverride: request.SceneOverride);
            trace.Data["actorName"] = selectedActor.Name;
            FinalizeTrace(trace, "completed");
            await ReportProgressAsync(progress, trace);

            var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(plan.PrivateIntent) && !string.IsNullOrWhiteSpace(selectedActor.Id))
                privateIntents[selectedActor.Id] = plan.PrivateIntent;

            var scene = SessionCloner.Clone(request.SceneOverride ?? TranscriptGraph.GetSceneForNextTurn(document.Transcript, request.ParentTurnId));
            return new(
                selectedActor.Id,
                selectedActor.Name,
                CreateTurnPlan(plan, context.RequestedTurnShape),
                appearance,
                privateIntents,
                scene,
                prose,
                trace);
        }
        catch (Exception exception) when (exception is not TranscriptGenerationException)
        {
            FailRunningStep(trace, exception.Message);
            FinalizeTrace(trace, "failed");
            trace.Data["error"] = exception.Message;
            await ReportProgressAsync(progress, trace);
            throw new TranscriptGenerationException(exception.Message, trace);
        }
    }

    public async Task<GeneratedTurnResult> GeneratePlanAndProseAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GeneratePlanAndProseRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers, modelSelections);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ProviderType = selection.Provider.Type,
            ModelId = selection.Model.Id
        };
        try
        {
            if (!selection.Capabilities.CanGenerateStructuredText)
                throw new InvalidOperationException("Planning a new branch failed because the reasoning model has structured output disabled.");

            (string Id, string Name) actor = request.RequestedNarrator
                ? ("", "Narrator")
                : (request.ActorCharacterId, string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName);
            var turnRequest = new GenerateTurnRequest(
                request.ParentTurnId,
                request.Mode,
                request.Guidance,
                request.RequestedTurnShape,
                actor.Id,
                actor.Name,
                request.RequestedNarrator,
                request.Scene);
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                request.RequestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == actor.Id),
                request.RequestedNarrator,
                request.Scene,
                request.AppearanceByCharacterId);
            SeedPlanningAndProseSteps(trace, selection);
            await ReportProgressAsync(progress, trace);
            var plan = await RunPlanningStepAsync(document, selection, context, actor, trace, progress, cancellationToken);
            var prose = await RunProseStepAsync(document, providers, modelSelections, selection, turnRequest, context, actor, plan, trace, progress, cancellationToken, progressSceneOverride: request.Scene);
            trace.Data["actorName"] = actor.Name;
            FinalizeTrace(trace, "completed");
            await ReportProgressAsync(progress, trace);

            var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(plan.PrivateIntent) && !string.IsNullOrWhiteSpace(actor.Id))
                privateIntents[actor.Id] = plan.PrivateIntent;

            return new(
                actor.Id,
                actor.Name,
                CreateTurnPlan(plan, context.RequestedTurnShape),
                CloneMap(request.AppearanceByCharacterId),
                privateIntents,
                SessionCloner.Clone(request.Scene),
                prose,
                trace);
        }
        catch (Exception exception) when (exception is not TranscriptGenerationException)
        {
            FailRunningStep(trace, exception.Message);
            FinalizeTrace(trace, "failed");
            trace.Data["error"] = exception.Message;
            await ReportProgressAsync(progress, trace);
            throw new TranscriptGenerationException(exception.Message, trace);
        }
    }

    public async Task<GeneratedTurnResult> GenerateProseFromPlanAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateProseFromPlanRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers, modelSelections);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ProviderType = selection.Provider.Type,
            ModelId = selection.Model.Id
        };
        try
        {
            var plan = SessionCloner.Clone(request.Plan);
            (string Id, string Name) actor = request.RequestedNarrator
                ? ("", "Narrator")
                : (request.ActorCharacterId, string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName);
            var turnRequest = new GenerateTurnRequest(
                request.ParentTurnId,
                request.Mode,
                request.Guidance,
                plan.TurnShape,
                actor.Id,
                actor.Name,
                request.RequestedNarrator,
                request.Scene);
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                plan.TurnShape,
                document.Characters.FirstOrDefault(character => character.Id == actor.Id),
                request.RequestedNarrator,
                request.Scene,
                request.AppearanceByCharacterId);
            var planner = CreatePlanningResponse(plan, ResolvePrivateIntent(request.PrivateIntentByCharacterId, actor.Id));
            SeedTurnSteps(trace, selection, false);
            await ReportProgressAsync(progress, trace);
            var prose = await RunProseStepAsync(document, providers, modelSelections, selection, turnRequest, context, actor, planner, trace, progress, cancellationToken, plan, request.Scene);
            trace.Data["actorName"] = actor.Name;
            FinalizeTrace(trace, "completed");
            await ReportProgressAsync(progress, trace);

            return new(
                actor.Id,
                actor.Name,
                plan,
                CloneMap(request.AppearanceByCharacterId),
                CloneMap(request.PrivateIntentByCharacterId),
                SessionCloner.Clone(request.Scene),
                prose,
                trace);
        }
        catch (Exception exception) when (exception is not TranscriptGenerationException)
        {
            FailRunningStep(trace, exception.Message);
            FinalizeTrace(trace, "failed");
            trace.Data["error"] = exception.Message;
            await ReportProgressAsync(progress, trace);
            throw new TranscriptGenerationException(exception.Message, trace);
        }
    }

    public async Task<GeneratedSnapshotResult> GenerateSnapshotAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveSnapshotModel(providers, modelSelections);
        var trace = new RpTurnTrace
        {
            Status = "running",
            StartedUtc = DateTime.UtcNow,
            ProviderId = selection.Provider.Id,
            ProviderName = selection.Provider.Name,
            ProviderType = selection.Provider.Type,
            ModelId = selection.Model.Id
        };
        try
        {
            if (!selection.Capabilities.CanGenerateStructuredText)
                throw new InvalidOperationException("Creating a snapshot failed because the reasoning model has structured output disabled.");

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
            return new(
                ResolveSnapshotSummary(result),
                NormalizeSnapshotTimelineEntries(result.TimelineEntries),
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
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        ActiveModelSelection selection,
        GenerateTurnRequest request,
        TurnPromptContext context,
        RpTurnTrace trace,
        TranscriptGenerationProgress? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RequestedActorCharacterId) && !request.RequestedNarrator)
            throw new InvalidOperationException("Generating transcript prose failed because Respond As is required when the active model has structured output disabled.");

        (string Id, string Name) actor = request.RequestedNarrator
            ? ("", "Narrator")
            : (request.RequestedActorCharacterId, request.RequestedActorName);
        var plan = CreateDumbProsePlan(context, request);
        var prose = await RunProseStepAsync(document, providers, modelSelections, selection, request, context, actor, plan, trace, progress, cancellationToken, progressSceneOverride: request.SceneOverride);
        trace.Data["actorName"] = actor.Name;
        FinalizeTrace(trace, "completed");
        await ReportProgressAsync(progress, trace);

        var scene = SessionCloner.Clone(request.SceneOverride ?? TranscriptGraph.GetSceneForNextTurn(document.Transcript, request.ParentTurnId));
        return new(
            actor.Id,
            actor.Name,
            CreateTurnPlan(plan, context.RequestedTurnShape),
            [],
            [],
            scene,
            prose,
            trace);
    }

    async Task<Dictionary<string, string>> RunAppearanceStepAsync(
        RpChatDocument document,
        ActiveModelSelection selection,
        TurnPromptContext context,
        RpTurnTrace trace,
        TranscriptGenerationProgress? progress,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "appearance");
        var tokens = promptContextBuilder.BuildTokens(context, "", PromptLibraryStageIds.Appearance);
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Appearance, tokens);
        var startedUtc = DateTime.UtcNow;
        await StartStepAsync(trace, "appearance", selection, startedUtc, progress);
        var completion = await SendStructuredAsync<AppearanceResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Generating appearance state", cancellationToken);
        var result = completion.Value;
        await CompleteStepAsync(trace, CreateStepTrace(
            "appearance",
            "Appearance",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
            ""), progress);
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
        ActiveModelSelection selection,
        TurnPromptContext context,
        RpTurnTrace trace,
        TranscriptGenerationProgress? progress,
        CancellationToken cancellationToken)
    {
        var startedUtc = DateTime.UtcNow;
        await StartStepAsync(trace, "selection", selection, startedUtc, progress);
        var tuning = ResolveTuning(document.ModelTuning, "selection");
        var tokens = promptContextBuilder.BuildTokens(context, "", PromptLibraryStageIds.Selection);
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Selection, tokens);
        var completion = await SendStructuredAsync<SelectionResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Selecting transcript actor", cancellationToken);
        var result = completion.Value;
        await CompleteStepAsync(trace, CreateStepTrace(
            "selection",
            "Selection",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
            ""), progress);
        var actorId = ResolveCharacterId(document.Characters, result.CharacterName);
        var actorName = document.Characters.FirstOrDefault(character => character.Id == actorId)?.Name ?? result.CharacterName;
        return (actorId, actorName);
    }

    async Task<PlanningResponse> RunPlanningStepAsync(
        RpChatDocument document,
        ActiveModelSelection selection,
        TurnPromptContext context,
        (string Id, string Name) actor,
        RpTurnTrace trace,
        TranscriptGenerationProgress? progress,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, "planning");
        var tokens = promptContextBuilder.BuildTokens(context with { Actor = document.Characters.FirstOrDefault(character => character.Id == actor.Id) }, "", PromptLibraryStageIds.Planning);
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Planning, tokens);
        var startedUtc = DateTime.UtcNow;
        await StartStepAsync(trace, "planning", selection, startedUtc, progress);
        var completion = await SendStructuredAsync<PlanningResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Planning transcript turn", cancellationToken);
        var result = completion.Value;
        await CompleteStepAsync(trace, CreateStepTrace(
            "planning",
            "Planning",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
            ""), progress);
        return result;
    }

    async Task<string> RunProseStepAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        ActiveModelSelection selection,
        GenerateTurnRequest request,
        TurnPromptContext context,
        (string Id, string Name) actor,
        PlanningResponse plan,
        RpTurnTrace trace,
        TranscriptGenerationProgress? progress,
        CancellationToken cancellationToken,
        RpTurnPlan? progressPlanOverride = null,
        RpSceneFrame? progressSceneOverride = null)
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
        var audioTagGuide = _audioTagGuideService.BuildGuide(document, providers, modelSelections);
        var systemPrompt = AppendPromptBlock(prompt.SystemPrompt, audioTagGuide.SystemGuide);
        var userPromptBody = AppendPromptBlock(prompt.UserPrompt, audioTagGuide.UserReminder);
        var userPrompt = context.Actor is null
            ? PromptLibraryService.WithNarratorProseFormatReminder(userPromptBody)
            : PromptLibraryService.WithProseFormatReminder(userPromptBody);
        var startedUtc = DateTime.UtcNow;
        await StartStepAsync(trace, "prose", selection, startedUtc, progress);
        var turnPlan = progressPlanOverride is null ? CreateTurnPlan(plan, context.RequestedTurnShape) : SessionCloner.Clone(progressPlanOverride);
        var scene = SessionCloner.Clone(progressSceneOverride ?? TranscriptGraph.GetSceneForNextTurn(document.Transcript, request.ParentTurnId));
        await ReportProseProgressAsync(progress, request, actor, turnPlan, scene, "");
        var completion = await SendStreamingTextAsync(
            selection,
            tuning,
            systemPrompt,
            userPrompt,
            "Writing transcript prose",
            async body => await ReportProseProgressAsync(progress, request, actor, turnPlan, scene, body),
            cancellationToken);
        await CompleteStepAsync(trace, CreateStepTrace(
            "prose",
            "Prose",
            selection,
            startedUtc,
            DateTime.UtcNow,
            systemPrompt,
            userPrompt,
            completion,
            "",
            ""), progress);
        return completion.Text.Trim();
    }

    static async Task ReportProseProgressAsync(
        TranscriptGenerationProgress? progress,
        GenerateTurnRequest request,
        (string Id, string Name) actor,
        RpTurnPlan plan,
        RpSceneFrame scene,
        string body)
    {
        if (progress is null)
            return;

        await progress.ReportProseAsync(new(
            request.ParentTurnId,
            request.Mode,
            request.Guidance,
            actor.Id,
            actor.Name,
            SessionCloner.Clone(plan),
            SessionCloner.Clone(scene),
            body));
    }

    static PlanningResponse CreateDumbProsePlan(TurnPromptContext context, GenerateTurnRequest request)
    {
        var guidance = string.IsNullOrWhiteSpace(request.Guidance)
            ? "Continue the scene in character."
            : request.Guidance.Trim();
        var turnShape = TurnShapeRules.NormalizeExplicitLabel(context.RequestedTurnShape);
        return new()
        {
            TurnShape = turnShape,
            Beat = guidance,
            Intent = "Write the next prose turn.",
            ImmediateGoal = "Continue the active exchange.",
            WhyNow = "The user requested the next turn.",
            ChangeIntroduced = "Continue the scene without structured planning.",
            Guardrails = $"Turn shape: {turnShape}. Stay grounded in the visible scene."
        };
    }

    static PlanningResponse CreatePlanningResponse(RpTurnPlan plan, string privateIntent) => new()
    {
        TurnShape = plan.TurnShape,
        Beat = plan.Beat,
        Intent = plan.Intent,
        ImmediateGoal = plan.ImmediateGoal,
        WhyNow = plan.WhyNow,
        ChangeIntroduced = plan.ChangeIntroduced,
        Guardrails = plan.Guardrails,
        PrivateIntent = privateIntent
    };

    static string ResolvePrivateIntent(IReadOnlyDictionary<string, string> privateIntents, string actorId) =>
        !string.IsNullOrWhiteSpace(actorId) && privateIntents.TryGetValue(actorId, out var privateIntent)
            ? privateIntent
            : "";

    static Dictionary<string, string> CloneMap(IReadOnlyDictionary<string, string> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    static ModelTuningStepState ResolveTuning(ModelTuningState state, string stepId)
    {
        if (state.Values.TryGetValue(stepId, out var tuning))
            return tuning;

        var defaults = ModelTuningState.CreateDefault();
        return defaults.Values.TryGetValue(stepId, out var defaultTuning) ? defaultTuning : new();
    }

    async Task<ModelStructuredCompletion<T>> SendStructuredAsync<T>(
        ActiveModelSelection selection,
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
        ActiveModelSelection selection,
        ModelTuningStepState tuning,
        string systemPrompt,
        string userPrompt,
        string operationName,
        Func<string, Task> textChanged,
        CancellationToken cancellationToken)
    {
        var request = new ModelGenerationRequest(
            selection.Provider,
            selection.Model,
            selection.Capabilities,
            tuning,
            systemPrompt,
            userPrompt,
            operationName);
        var text = new StringBuilder();
        var inputTokens = 0;
        var outputTokens = 0;
        var responseId = "";
        await foreach (var update in generationClient.GenerateStreamingTextUpdatesAsync(request, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(update.TextDelta))
            {
                text.Append(update.TextDelta);
                await textChanged(text.ToString());
            }

            if (!update.Completed)
                continue;

            inputTokens = update.InputTokens;
            outputTokens = update.OutputTokens;
            responseId = update.ResponseId;
        }

        return new(text.ToString(), inputTokens, outputTokens, responseId);
    }

    static RpTurnPlan CreateTurnPlan(PlanningResponse plan, string requestedTurnShape) => new()
    {
        TurnShape = ResolveTurnShape(plan.TurnShape, requestedTurnShape),
        Beat = plan.Beat,
        Intent = plan.Intent,
        ImmediateGoal = plan.ImmediateGoal,
        WhyNow = plan.WhyNow,
        ChangeIntroduced = plan.ChangeIntroduced,
        Guardrails = plan.Guardrails
    };

    static RpTurnTraceStep CreateStepTrace(
        string id,
        string label,
        ActiveModelSelection selection,
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
        ProviderType = selection.Provider.Type,
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

    static bool NeedsSelectionStep(GenerateTurnRequest request) =>
        !request.RequestedNarrator && string.IsNullOrWhiteSpace(request.RequestedActorCharacterId);

    static (string Id, string Name) ResolveRequestedActor(GenerateTurnRequest request) =>
        request.RequestedNarrator
            ? ("", "Narrator")
            : (request.RequestedActorCharacterId, request.RequestedActorName);

    static void SeedTurnSteps(RpTurnTrace trace, ActiveModelSelection selection, bool structured, bool includeSelection = true)
    {
        var steps = structured
            ? includeSelection
                ? new[] { ("appearance", "Appearance"), ("selection", "Selection"), ("planning", "Planning"), ("prose", "Prose") }
                : new[] { ("appearance", "Appearance"), ("planning", "Planning"), ("prose", "Prose") }
            : new[] { ("prose", "Prose") };

        SeedSteps(trace, selection, steps);
    }

    static void SeedPlanningAndProseSteps(RpTurnTrace trace, ActiveModelSelection selection) =>
        SeedSteps(trace, selection, [("planning", "Planning"), ("prose", "Prose")]);

    static void SeedSteps(RpTurnTrace trace, ActiveModelSelection selection, IReadOnlyList<(string Id, string Label)> steps)
    {
        foreach (var (id, label) in steps)
            trace.Steps.Add(new()
            {
                Id = id,
                Label = label,
                Status = "pending",
                ProviderId = selection.Provider.Id,
                ProviderName = selection.Provider.Name,
                ProviderType = selection.Provider.Type,
                ModelId = selection.Model.Id
            });

        trace.Summary = $"Generating · {string.Join(" -> ", trace.Steps.Select(step => step.Label))}";
    }

    static async Task StartStepAsync(RpTurnTrace trace, string stepId, ActiveModelSelection selection, DateTime startedUtc, TranscriptGenerationProgress? progress)
    {
        var step = FindStep(trace, stepId);
        step.Status = "running";
        step.StartedUtc = startedUtc;
        step.CompletedUtc = default;
        step.ProviderId = selection.Provider.Id;
        step.ProviderName = selection.Provider.Name;
        step.ProviderType = selection.Provider.Type;
        step.ModelId = selection.Model.Id;
        trace.Summary = $"Generating · {string.Join(" -> ", trace.Steps.Select(item => item.Label))}";
        await ReportProgressAsync(progress, trace);
    }

    static async Task CompleteStepAsync(RpTurnTrace trace, RpTurnTraceStep completedStep, TranscriptGenerationProgress? progress)
    {
        var step = FindStep(trace, completedStep.Id);
        CopyStep(completedStep, step);
        trace.Summary = $"Generating · {string.Join(" -> ", trace.Steps.Select(item => item.Label))}";
        await ReportProgressAsync(progress, trace);
    }

    static RpTurnTraceStep FindStep(RpTurnTrace trace, string stepId)
    {
        var step = trace.Steps.FirstOrDefault(step => step.Id == stepId);
        if (step is not null)
            return step;

        step = new() { Id = stepId, Label = stepId };
        trace.Steps.Add(step);
        return step;
    }

    static void CopyStep(RpTurnTraceStep source, RpTurnTraceStep target)
    {
        target.Id = source.Id;
        target.Label = source.Label;
        target.Status = source.Status;
        target.StartedUtc = source.StartedUtc;
        target.CompletedUtc = source.CompletedUtc;
        target.ProviderId = source.ProviderId;
        target.ProviderName = source.ProviderName;
        target.ProviderType = source.ProviderType;
        target.ModelId = source.ModelId;
        target.InputTokens = source.InputTokens;
        target.OutputTokens = source.OutputTokens;
        target.TotalTokens = source.TotalTokens;
        target.DurationSeconds = source.DurationSeconds;
        target.SystemPrompt = source.SystemPrompt;
        target.UserPrompt = source.UserPrompt;
        target.RawOutput = source.RawOutput;
        target.StructuredOutputJson = source.StructuredOutputJson;
        target.Error = source.Error;
        target.Data = source.Data.DeepClone().AsObject();
    }

    static void FailRunningStep(RpTurnTrace trace, string error)
    {
        var step = trace.Steps.FirstOrDefault(step => step.Status == "running")
            ?? trace.Steps.FirstOrDefault(step => step.Status == "pending");
        if (step is null)
            return;

        if (step.StartedUtc == default)
            step.StartedUtc = DateTime.UtcNow;

        step.Status = "failed";
        step.CompletedUtc = DateTime.UtcNow;
        step.DurationSeconds = (step.CompletedUtc - step.StartedUtc).TotalSeconds;
        step.Error = error;
    }

    static Task ReportProgressAsync(TranscriptGenerationProgress? progress, RpTurnTrace trace) =>
        progress?.ReportAsync(trace) ?? Task.CompletedTask;

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

    static ActiveModelSelection ResolveTextModel(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections) =>
        TextModelTuningCatalog.TryResolveActiveTextModel(providers, modelSelections)
        ?? throw new InvalidOperationException("Generating transcript text failed because no text-capable model is enabled.");

    static ActiveModelSelection ResolveSnapshotModel(IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections) =>
        TextModelTuningCatalog.TryResolveActiveReasoningModel(providers, modelSelections)
        ?? throw new InvalidOperationException("Creating a snapshot failed because no reasoning model is enabled.");

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

    static string AppendPromptBlock(string prompt, string block) =>
        string.IsNullOrWhiteSpace(block)
            ? prompt
            : $"{prompt.TrimEnd()}{Environment.NewLine}{Environment.NewLine}{block.Trim()}";

    static string ResolveTurnShape(string requestedByPlanner, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(requestedByPlanner) ? fallback : requestedByPlanner;
        return TurnShapeRules.NormalizeExplicitLabel(value);
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

    static List<RpTranscriptSnapshotTimelineEntry> NormalizeSnapshotTimelineEntries(IReadOnlyList<SnapshotTimelineEntryResponse>? entries) =>
        entries?
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
            .Select(entry => new RpTranscriptSnapshotTimelineEntry
            {
                TurnNumber = entry.TurnNumber,
                Title = entry.Title.Trim(),
                Description = entry.Description.Trim(),
                CharacterNames = NormalizeNames(entry.CharacterNames),
                LocationNames = NormalizeNames(entry.LocationNames),
                ItemNames = NormalizeNames(entry.ItemNames)
            })
            .ToList()
        ?? [];

    static List<string> NormalizeNames(IReadOnlyList<string>? names) =>
        names?.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

    public sealed class SnapshotTimelineEntryResponse
    {
        public int TurnNumber { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public IReadOnlyList<string>? CharacterNames { get; set; }
        public IReadOnlyList<string>? LocationNames { get; set; }
        public IReadOnlyList<string>? ItemNames { get; set; }
    }

    sealed class SnapshotResponse
    {
        public string NarrativeSummary { get; set; } = "";
        public string Summary { get; set; } = "";
        public IReadOnlyList<SnapshotTimelineEntryResponse>? TimelineEntries { get; set; }
    }
}
