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

public sealed record SelectCyoaActorRequest(
    string ParentTurnId,
    IReadOnlyList<string> ControlledCharacterIds,
    bool ForceControlled);

public sealed record CyoaActorSelection(
    string ActorCharacterId,
    string ActorName,
    bool RequestedNarrator);

public sealed record GenerateCyoaDecisionRequest(
    string ParentTurnId,
    string Mode,
    string ActorCharacterId,
    string ActorName,
    bool RequestedNarrator);

public sealed record GeneratedCyoaDecision(
    RpCyoaPendingDecision Decision,
    RpGenerationTrace Trace);

public sealed record GenerateSelectedCyoaTurnRequest(
    RpCyoaPendingDecision Decision,
    RpCyoaOption? Option,
    string CustomGuidance);

public sealed record GenerateAutonomousCyoaTurnRequest(
    string ParentTurnId,
    string Mode,
    string ActorCharacterId,
    string ActorName,
    bool RequestedNarrator);

public sealed record GenerateSnapshotRequest(string TurnId);

public sealed record GeneratedTurnResult(
    string ActorCharacterId,
    string ActorName,
    RpTurnPlan Plan,
    Dictionary<string, string> AppearanceByCharacterId,
    Dictionary<string, string> PrivateIntentByCharacterId,
    RpSceneFrame Scene,
    string Body,
    RpGenerationTrace Trace);

public sealed record GeneratedSnapshotResult(
    string Summary,
    List<RpTranscriptSnapshotTimelineEntry> TimelineEntries,
    RpGenerationTrace Trace,
    Dictionary<string, string>? CharacterSceneStates = null,
    RpSceneFrame? Scene = null,
    List<RpTranscriptSnapshotRelationshipUpdate>? RelationshipUpdates = null);

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
    Task<CyoaActorSelection> SelectCyoaActorAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, SelectCyoaActorRequest request, CancellationToken cancellationToken = default);
    Task<GeneratedCyoaDecision> GenerateCyoaDecisionAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateCyoaDecisionRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default);
    Task<GeneratedTurnResult> GenerateSelectedCyoaTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateSelectedCyoaTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default);
    Task<GeneratedTurnResult> GenerateAutonomousCyoaTurnAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateAutonomousCyoaTurnRequest request, TranscriptGenerationProgress? progress = null, CancellationToken cancellationToken = default);
    Task<GeneratedSnapshotResult> GenerateSnapshotAsync(RpChatDocument document, IReadOnlyList<AiProvider> providers, ActiveModelSelectionsState modelSelections, GenerateSnapshotRequest request, CancellationToken cancellationToken = default);
}

public sealed record TranscriptGenerationProgress(Func<RpGenerationTrace, Task> OnChanged, Func<TranscriptProseUpdate, Task>? OnProseChanged = null)
{
    public Task ReportAsync(RpGenerationTrace trace) => OnChanged(trace);
    public Task ReportProseAsync(TranscriptProseUpdate update) => OnProseChanged?.Invoke(update) ?? Task.CompletedTask;
}

public sealed class TranscriptGenerationException(string message, RpGenerationTrace trace) : Exception(message)
{
    public RpGenerationTrace Trace { get; } = trace;
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
        var trace = new RpGenerationTrace
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

            var continuity = await RunSceneContinuityStepAsync(document, selection, context, request, trace, progress, cancellationToken);
            var selectedActor = useSelectionStep
                ? await RunSelectionStepAsync(document, selection, BuildContextWithScene(document, request, continuity.Scene, continuity.CharacterSceneStates, null), trace, progress, cancellationToken)
                : ResolveRequestedActor(request);
            var selectedContext = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                request.Guidance,
                request.RequestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == selectedActor.Id),
                request.RequestedNarrator,
                continuity.Scene,
                continuity.CharacterSceneStates);
            var plan = await RunPlanningStepAsync(document, selection, selectedContext, selectedActor, trace, progress, cancellationToken);
            var prose = await RunProseStepAsync(document, providers, modelSelections, selection, request, selectedContext, selectedActor, plan, trace, progress, cancellationToken, progressSceneOverride: continuity.Scene);
            trace.Data["actorName"] = selectedActor.Name;
            FinalizeTrace(trace, "completed");
            await ReportProgressAsync(progress, trace);

            var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(plan.PrivateIntent) && !string.IsNullOrWhiteSpace(selectedActor.Id))
                privateIntents[selectedActor.Id] = plan.PrivateIntent;

            return new(
                selectedActor.Id,
                selectedActor.Name,
                CreateTurnPlan(plan, context.RequestedTurnShape),
                continuity.CharacterSceneStates,
                privateIntents,
                continuity.Scene,
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
        var trace = new RpGenerationTrace
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
        var trace = new RpGenerationTrace
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

    public async Task<CyoaActorSelection> SelectCyoaActorAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        SelectCyoaActorRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers, modelSelections);
        if (!selection.Capabilities.CanGenerateStructuredText)
            return FallbackCyoaActor(document, request);

        var scene = TranscriptGraph.GetSceneForNextTurn(document.Transcript, request.ParentTurnId);
        var presentCharacters = document.Characters
            .Where(character => scene.InSceneCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            .ToList();
        var candidates = request.ForceControlled
            ? presentCharacters.Where(character => request.ControlledCharacterIds.Contains(character.Id, StringComparer.Ordinal)).ToList()
            : presentCharacters;
        if (candidates.Count == 0)
            return FallbackCyoaActor(document, request);

        var active = TranscriptGraph.FindTurn(document.Transcript, request.ParentTurnId);
        var tuning = ResolveTuning(document.ModelTuning, PromptLibraryStageIds.Selection);
        var systemPrompt = """
            Choose who should act next in this fictional scene.
            Return only structured data.

            Choose exactly one candidate by name.
            Do not choose the active speaker when another candidate has a reasonable reason to act.
            If forceControlled is true, choose only from the controlled candidates.
            """;
        var userPrompt = $"""
            forceControlled: {request.ForceControlled}
            Active speaker: {PromptText(active?.AuthorName, "None")}

            Candidates:
            {FormatCyoaActorCandidates(candidates, request.ControlledCharacterIds)}

            Recent transcript:
            {FormatCyoaRecentTranscript(document, request.ParentTurnId)}
            """;
        var completion = await SendStructuredAsync<CyoaActorSelectionResponse>(
            selection,
            tuning,
            systemPrompt,
            userPrompt,
            "Selecting CYOA actor",
            cancellationToken);
        var actor = ResolveCharacter(document.Characters, completion.Value.CharacterName);
        if (actor is null || !candidates.Any(candidate => string.Equals(candidate.Id, actor.Id, StringComparison.Ordinal)))
            return FallbackCyoaActor(document, request);

        return new(actor.Id, actor.Name, false);
    }

    public async Task<GeneratedCyoaDecision> GenerateCyoaDecisionAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateCyoaDecisionRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers, modelSelections);
        var trace = new RpGenerationTrace
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
                throw new InvalidOperationException("Generating CYOA choices failed because the reasoning model has structured output disabled.");

            SeedSteps(trace, selection, [("cyoa-options", "Choices")]);
            await ReportProgressAsync(progress, trace);
            var scene = SessionCloner.Clone(TranscriptGraph.GetSceneForNextTurn(document.Transcript, request.ParentTurnId));
            var actor = request.RequestedNarrator
                ? null
                : document.Characters.FirstOrDefault(character => string.Equals(character.Id, request.ActorCharacterId, StringComparison.Ordinal));
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                "",
                TurnShapeRules.AutoLabel,
                actor,
                request.RequestedNarrator,
                scene);
            var tokens = promptContextBuilder.BuildTokens(context, "", PromptLibraryStageIds.Planning);
            var tuning = ResolveTuning(document.ModelTuning, PromptLibraryStageIds.Planning);
            var prompt = BuildCyoaDecisionPrompt(document, request, tokens["{context}"], scene);
            var startedUtc = DateTime.UtcNow;
            await StartStepAsync(trace, "cyoa-options", selection, startedUtc, progress);
            var completion = await SendStructuredAsync<CyoaDecisionResponse>(
                selection,
                tuning,
                prompt.SystemPrompt,
                prompt.UserPrompt,
                "Generating CYOA choices",
                cancellationToken);
            var decision = BuildCyoaDecision(document, request, scene, completion.Value);
            await CompleteStepAsync(trace, CreateStepTrace(
                "cyoa-options",
                "Choices",
                selection,
                startedUtc,
                DateTime.UtcNow,
                prompt.SystemPrompt,
                prompt.UserPrompt,
                completion,
                JsonSerializer.Serialize(completion.Value, AppJsonSerializerOptions.IndentedWeb),
                ""), progress);
            trace.Data["actorName"] = string.IsNullOrWhiteSpace(request.ActorName) ? "Narrator" : request.ActorName;
            FinalizeTrace(trace, "completed");
            decision.Trace = SessionCloner.Clone(trace);
            await ReportProgressAsync(progress, trace);
            return new(decision, trace);
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

    public async Task<GeneratedTurnResult> GenerateSelectedCyoaTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateSelectedCyoaTurnRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers, modelSelections);
        var trace = CreateSelectedCyoaTrace(request.Decision, selection);
        try
        {
            if (!selection.Capabilities.CanGenerateStructuredText)
                throw new InvalidOperationException("Generating the selected CYOA turn failed because the reasoning model has structured output disabled.");

            var option = request.Option;
            var requestedNarrator = option?.RequestedNarrator ?? request.Decision.RequestedNarrator;
            (string Id, string Name) actor = requestedNarrator
                ? ("", "Narrator")
                : (
                    option?.ActorCharacterId ?? request.Decision.ActorCharacterId,
                    string.IsNullOrWhiteSpace(option?.ActorName) ? request.Decision.ActorName : option.ActorName);
            if (string.IsNullOrWhiteSpace(actor.Name))
                actor.Name = "Narrator";

            var guidance = ResolveSelectedCyoaGuidance(request);
            var planningGuidance = BuildSelectedCyoaPlanningGuidance(guidance, option);
            var requestedTurnShape = ResolveTurnShape(option?.Plan.TurnShape ?? "", TurnShapeRules.AutoLabel);
            var turnRequest = new GenerateTurnRequest(
                request.Decision.ParentTurnId,
                "guided",
                guidance,
                requestedTurnShape,
                actor.Id,
                actor.Name,
                requestedNarrator);
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.Decision.ParentTurnId,
                planningGuidance,
                requestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == actor.Id),
                requestedNarrator);

            SeedSelectedCyoaTurnSteps(trace, selection);
            await ReportProgressAsync(progress, trace);

            var continuity = await RunSceneContinuityStepAsync(document, selection, context, turnRequest, trace, progress, cancellationToken);
            var selectedContext = promptContextBuilder.BuildTurnContext(
                document,
                request.Decision.ParentTurnId,
                planningGuidance,
                requestedTurnShape,
                document.Characters.FirstOrDefault(character => character.Id == actor.Id),
                requestedNarrator,
                continuity.Scene,
                continuity.CharacterSceneStates);
            var plan = await RunPlanningStepAsync(document, selection, selectedContext, actor, trace, progress, cancellationToken);
            var prose = await RunProseStepAsync(document, providers, modelSelections, selection, turnRequest, selectedContext, actor, plan, trace, progress, cancellationToken, progressSceneOverride: continuity.Scene);
            trace.Data["actorName"] = actor.Name;
            FinalizeTrace(trace, "completed");
            await ReportProgressAsync(progress, trace);

            var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(plan.PrivateIntent) && !string.IsNullOrWhiteSpace(actor.Id))
                privateIntents[actor.Id] = plan.PrivateIntent;

            return new(
                actor.Id,
                actor.Name,
                CreateTurnPlan(plan, selectedContext.RequestedTurnShape),
                continuity.CharacterSceneStates,
                privateIntents,
                continuity.Scene,
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

    public async Task<GeneratedTurnResult> GenerateAutonomousCyoaTurnAsync(
        RpChatDocument document,
        IReadOnlyList<AiProvider> providers,
        ActiveModelSelectionsState modelSelections,
        GenerateAutonomousCyoaTurnRequest request,
        TranscriptGenerationProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        ApplyCapabilities(providers);
        var selection = ResolveTextModel(providers, modelSelections);
        var trace = new RpGenerationTrace
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
            var baseContext = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                "",
                TurnShapeRules.AutoLabel,
                document.Characters.FirstOrDefault(character => character.Id == request.ActorCharacterId),
                request.RequestedNarrator);
            SeedTurnSteps(trace, selection, selection.Capabilities.CanGenerateStructuredText, false);
            await ReportProgressAsync(progress, trace);
            if (!selection.Capabilities.CanGenerateStructuredText)
            {
                var dumbTurnRequest = new GenerateTurnRequest(
                    request.ParentTurnId,
                    request.Mode,
                    "",
                    TurnShapeRules.AutoLabel,
                    request.ActorCharacterId,
                    request.ActorName,
                    request.RequestedNarrator);
                return await GenerateDumbProseTurnAsync(document, providers, modelSelections, selection, dumbTurnRequest, baseContext, trace, progress, cancellationToken);
            }

            var turnRequestForContinuity = new GenerateTurnRequest(
                request.ParentTurnId,
                request.Mode,
                "",
                TurnShapeRules.AutoLabel,
                request.ActorCharacterId,
                request.ActorName,
                request.RequestedNarrator);
            var continuity = await RunSceneContinuityStepAsync(document, selection, baseContext, turnRequestForContinuity, trace, progress, cancellationToken);
            var actor = request.RequestedNarrator ? ("", "Narrator") : (request.ActorCharacterId, request.ActorName);
            var direction = await RunAutonomousDirectionChoiceAsync(document, selection, baseContext, actor, cancellationToken);
            if (direction == RpCyoaDirections.FastForward)
                direction = RpCyoaDirections.Pivot;

            var directionGuidance = BuildDirectionGuidance(direction, autonomous: true);
            var context = promptContextBuilder.BuildTurnContext(
                document,
                request.ParentTurnId,
                directionGuidance,
                TurnShapeRules.AutoLabel,
                document.Characters.FirstOrDefault(character => character.Id == request.ActorCharacterId),
                request.RequestedNarrator,
                continuity.Scene,
                continuity.CharacterSceneStates);
            var plan = await RunPlanningStepAsync(document, selection, context, actor, trace, progress, cancellationToken);
            var turnRequest = new GenerateTurnRequest(
                request.ParentTurnId,
                request.Mode,
                "",
                TurnShapeRules.AutoLabel,
                actor.Item1,
                actor.Item2,
                request.RequestedNarrator,
                continuity.Scene);
            var prose = await RunProseStepAsync(document, providers, modelSelections, selection, turnRequest, context, actor, plan, trace, progress, cancellationToken, progressSceneOverride: continuity.Scene);
            trace.Data["actorName"] = actor.Item2;
            FinalizeTrace(trace, "completed");
            await ReportProgressAsync(progress, trace);

            var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(plan.PrivateIntent) && !string.IsNullOrWhiteSpace(actor.Item1))
                privateIntents[actor.Item1] = plan.PrivateIntent;

            return new(
                actor.Item1,
                actor.Item2,
                CreateTurnPlan(plan, context.RequestedTurnShape),
                continuity.CharacterSceneStates,
                privateIntents,
                continuity.Scene,
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
        var trace = new RpGenerationTrace
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

            var continuityContext = promptContextBuilder.BuildTurnContext(
                document,
                request.TurnId,
                "",
                "Brief",
                null,
                requestedNarrator: true);
            var continuityRequest = new GenerateTurnRequest(
                request.TurnId,
                "snapshot",
                "",
                "Brief",
                "",
                "Narrator",
                RequestedNarrator: true);
            var continuity = await RunSceneContinuityStepAsync(document, selection, continuityContext, continuityRequest, trace, null, cancellationToken);
            var context = promptContextBuilder.BuildSnapshotContext(document, request.TurnId);
            var tuning = ResolveTuning(document.ModelTuning, "snapshot");
            var tokens = promptContextBuilder.BuildTokens(context);
            var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.Snapshot, tokens);
            var startedUtc = DateTime.UtcNow;
            var completion = await SendStructuredAsync<SnapshotResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Generating snapshot", cancellationToken);
            var result = completion.Value;
            var relationshipUpdates = NormalizeSnapshotRelationshipUpdates(
                result.RelationshipUpdates,
                document,
                CharacterTraitLibraryService.NormalizeState(document.CharacterTraitLibrary));
            trace.Data["relationshipUpdateCount"] = relationshipUpdates.Count;
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
                trace,
                continuity.CharacterSceneStates,
                continuity.Scene,
                relationshipUpdates);
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
        RpGenerationTrace trace,
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

    async Task<SceneContinuityResult> RunSceneContinuityStepAsync(
        RpChatDocument document,
        ActiveModelSelection selection,
        TurnPromptContext context,
        GenerateTurnRequest request,
        RpGenerationTrace trace,
        TranscriptGenerationProgress? progress,
        CancellationToken cancellationToken)
    {
        var tuning = ResolveTuning(document.ModelTuning, PromptLibraryStageIds.SceneContinuity);
        var tokens = promptContextBuilder.BuildTokens(context, "", PromptLibraryStageIds.SceneContinuity);
        var prompt = _promptLibraryService.Render(document.PromptLibrary, PromptLibraryStageIds.SceneContinuity, tokens);
        var startedUtc = DateTime.UtcNow;
        await StartStepAsync(trace, "scene-continuity", selection, startedUtc, progress);
        var completion = await SendStructuredAsync<AppearanceResponse>(selection, tuning, prompt.SystemPrompt, prompt.UserPrompt, "Reconciling scene continuity", cancellationToken);
        var result = completion.Value;
        await CompleteStepAsync(trace, CreateStepTrace(
            "scene-continuity",
            "Scene Continuity",
            selection,
            startedUtc,
            DateTime.UtcNow,
            prompt.SystemPrompt,
            prompt.UserPrompt,
            completion,
            JsonSerializer.Serialize(result, AppJsonSerializerOptions.IndentedWeb),
            ""), progress);
        var characterSceneStates = result.Characters
            ?.Where(character => !string.IsNullOrWhiteSpace(character.CharacterName))
            .Select(character => ResolveAppearanceCharacter(document.Characters, character))
            .Where(pair => pair is not null && !string.IsNullOrWhiteSpace(pair.Value.Appearance))
            .GroupBy(pair => pair!.Value.CharacterId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last()!.Value.Appearance, StringComparer.Ordinal)
            ?? new(StringComparer.Ordinal);
        var scene = SessionCloner.Clone(request.SceneOverride ?? TranscriptGraph.GetSceneForNextTurn(document.Transcript, request.ParentTurnId));
        scene.CharacterPhysicalStates = result.PhysicalStates is null
            ? scene.CharacterPhysicalStates
            : NormalizePhysicalStates(document.Characters, result.PhysicalStates);
        scene.SceneObjects = result.SceneObjects is null
            ? scene.SceneObjects
            : NormalizeSceneObjects(document.Characters, result.SceneObjects);
        return new(characterSceneStates, scene);
    }

    async Task<(string Id, string Name)> RunSelectionStepAsync(
        RpChatDocument document,
        ActiveModelSelection selection,
        TurnPromptContext context,
        RpGenerationTrace trace,
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

    TurnPromptContext BuildContextWithScene(
        RpChatDocument document,
        GenerateTurnRequest request,
        RpSceneFrame scene,
        IReadOnlyDictionary<string, string> characterSceneStates,
        RpCharacter? actor) =>
        promptContextBuilder.BuildTurnContext(
            document,
            request.ParentTurnId,
            request.Guidance,
            request.RequestedTurnShape,
            actor,
            request.RequestedNarrator,
            scene,
            characterSceneStates);

    async Task<PlanningResponse> RunPlanningStepAsync(
        RpChatDocument document,
        ActiveModelSelection selection,
        TurnPromptContext context,
        (string Id, string Name) actor,
        RpGenerationTrace trace,
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
        RpGenerationTrace trace,
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

    async Task<string> RunAutonomousDirectionChoiceAsync(
        RpChatDocument document,
        ActiveModelSelection selection,
        TurnPromptContext context,
        (string Id, string Name) actor,
        CancellationToken cancellationToken)
    {
        if (!selection.Capabilities.CanGenerateStructuredText)
            return RpCyoaDirections.Continue;

        var tuning = ResolveTuning(document.ModelTuning, PromptLibraryStageIds.Planning);
        var tokens = promptContextBuilder.BuildTokens(context with { Actor = document.Characters.FirstOrDefault(character => character.Id == actor.Id) }, "", PromptLibraryStageIds.Planning);
        var systemPrompt = """
            Choose the best direction category for the next autonomous character turn.
            Return only structured data.

            Allowed directions:
            - Continue: keep the current path moving.
            - Escalate: increase pressure, intimacy, risk, urgency, or emotional stakes.
            - Pivot: redirect focus, topic, tactic, or attention without jumping time or scene.
            - Fast Forward: suggest that the scene may soon skip time, wait, or move location.

            Autonomous Fast Forward is only a suggestion signal. Do not execute a time skip or scene change.
            """;
        var userPrompt = $"""
            Actor: {actor.Name}

            {tokens["{context}"]}

            Pick exactly one direction for this actor's next turn.
            """;
        var completion = await SendStructuredAsync<CyoaDirectionChoiceResponse>(
            selection,
            tuning,
            systemPrompt,
            userPrompt,
            "Choosing CYOA direction",
            cancellationToken);
        return RpCyoaDirections.Normalize(completion.Value.Direction);
    }

    static PromptRenderResult BuildCyoaDecisionPrompt(
        RpChatDocument document,
        GenerateCyoaDecisionRequest request,
        string context,
        RpSceneFrame scene)
    {
        var isDirector = string.Equals(request.Mode, RpCyoaModes.Director, StringComparison.Ordinal);
        var actorLine = isDirector
            ? "Director options may target the narrator or one present character."
            : $"All options are for {PromptText(request.ActorName, "the controlled character")}.";
        var systemPrompt = $"""
            You create inline choice options for a fictional story transcript.
            Return only structured data.

            Create exactly one option for each direction:
            - Continue
            - Escalate
            - Pivot
            - Fast Forward

            {actorLine}
            Each option needs a concise user-facing title, a short summary, guidance, and a concrete single-turn plan.
            Fast Forward options must propose a user-approved time skip, wait, location change, or scene reset; do not write prose for the jump.
            """;
        var userPrompt = $"""
            Mode: {request.Mode}
            Current actor: {(request.RequestedNarrator ? "Narrator" : PromptText(request.ActorName, "Unknown"))}

            Existing locations:
            {FormatReferenceNames(document.Locations.Select(location => location.Name))}

            Present characters:
            {FormatReferenceNames(document.Characters.Where(character => scene.InSceneCharacterIds.Contains(character.Id, StringComparer.Ordinal)).Select(character => character.Name))}

            Present items:
            {FormatReferenceNames(document.Items.Where(item => scene.InSceneItemIds.Contains(item.Id, StringComparer.Ordinal)).Select(item => item.Name))}

            {context}
            """;
        return new(systemPrompt, userPrompt);
    }

    static RpCyoaPendingDecision BuildCyoaDecision(
        RpChatDocument document,
        GenerateCyoaDecisionRequest request,
        RpSceneFrame scene,
        CyoaDecisionResponse response)
    {
        var optionsByDirection = response.Options
            .Select(option => (Direction: RpCyoaDirections.Normalize(option.Direction), Option: option))
            .GroupBy(pair => pair.Direction, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Option, StringComparer.Ordinal);
        var options = RpCyoaDirections.All
            .Select(direction => BuildCyoaOption(document, request, scene, direction, optionsByDirection.TryGetValue(direction, out var option) ? option : null))
            .ToList();
        return new()
        {
            Id = $"cyoa-{Guid.NewGuid():N}",
            ParentTurnId = request.ParentTurnId,
            Mode = request.Mode,
            ActorCharacterId = request.ActorCharacterId,
            ActorName = request.ActorName,
            RequestedNarrator = request.RequestedNarrator,
            CreatedUtc = DateTime.UtcNow,
            Options = options
        };
    }

    static RpCyoaOption BuildCyoaOption(
        RpChatDocument document,
        GenerateCyoaDecisionRequest request,
        RpSceneFrame scene,
        string direction,
        CyoaOptionResponse? response)
    {
        var isDirector = string.Equals(request.Mode, RpCyoaModes.Director, StringComparison.Ordinal);
        var requestedNarrator = request.RequestedNarrator;
        var actorId = request.ActorCharacterId;
        var actorName = request.ActorName;
        if (isDirector)
        {
            var target = ResolveCharacter(document.Characters, response?.ActorName ?? "");
            requestedNarrator = target is null;
            actorId = target?.Id ?? "";
            actorName = target?.Name ?? "Narrator";
        }

        var turnShape = ResolveTurnShape(response?.TurnShape ?? "", TurnShapeRules.BriefLabel);
        var title = FirstNonBlank(response?.Title, RpCyoaDirections.Label(direction));
        var summary = FirstNonBlank(response?.Summary, BuildDirectionGuidance(direction, autonomous: false));
        var guidance = FirstNonBlank(response?.Guidance, summary, BuildDirectionGuidance(direction, autonomous: false));
        var plan = new RpTurnPlan
        {
            TurnShape = turnShape,
            Beat = FirstNonBlank(response?.Beat, summary, $"{RpCyoaDirections.Label(direction)} the scene."),
            Intent = FirstNonBlank(response?.Intent, guidance, BuildDirectionGuidance(direction, autonomous: false)),
            ImmediateGoal = FirstNonBlank(response?.ImmediateGoal, "Make one clear playable move."),
            WhyNow = FirstNonBlank(response?.WhyNow, "The user selected this direction."),
            ChangeIntroduced = FirstNonBlank(response?.ChangeIntroduced, summary, "The scene moves forward."),
            Guardrails = FirstNonBlank(response?.Guardrails, "Keep the turn grounded in the current scene."),
            ContinuityIntents = NormalizeContinuityIntents(response?.ContinuityIntents)
        };
        var privateIntents = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!requestedNarrator && !string.IsNullOrWhiteSpace(response?.PrivateIntent))
            privateIntents[actorId] = response.PrivateIntent.Trim();

        return new()
        {
            Id = $"cyoa-option-{Guid.NewGuid():N}",
            Direction = direction,
            Title = title,
            Summary = summary,
            Guidance = guidance,
            ActorCharacterId = actorId,
            ActorName = actorName,
            RequestedNarrator = requestedNarrator,
            Plan = plan,
            PrivateIntentByCharacterId = privateIntents,
            Scene = SessionCloner.Clone(scene),
            SceneProposal = direction == RpCyoaDirections.FastForward
                ? BuildSceneProposal(document, scene, response, title, summary, guidance, plan)
                : null
        };
    }

    static RpCyoaSceneProposal BuildSceneProposal(
        RpChatDocument document,
        RpSceneFrame scene,
        CyoaOptionResponse? response,
        string title,
        string summary,
        string guidance,
        RpTurnPlan plan)
    {
        var location = ResolveLocation(document.Locations, response?.LocationName ?? "")
            ?? document.Locations.FirstOrDefault(location => string.Equals(location.Id, scene.LocationId, StringComparison.Ordinal))
            ?? document.Locations.FirstOrDefault();
        var characterIds = ResolveCharacterIds(document.Characters, response?.CharacterNames);
        if (characterIds.Count == 0)
            characterIds = [.. scene.InSceneCharacterIds];

        var itemIds = ResolveItemIds(document.Items, response?.ItemNames);
        if (itemIds.Count == 0)
            itemIds = [.. scene.InSceneItemIds];

        return new()
        {
            LocationId = location?.Id ?? scene.LocationId,
            LocationName = location?.Name ?? scene.LocationName,
            CharacterIds = characterIds,
            ItemIds = itemIds,
            Purpose = NormalizeScenePurpose(response?.ScenePurpose),
            Guidance = FirstNonBlank(
                response?.SceneGuidance,
                response?.Guidance,
                response?.Summary,
                response?.Beat,
                guidance,
                summary,
                plan.Beat,
                plan.Intent,
                title,
                "Move the scene forward in time while preserving established continuity.")
        };
    }

    static string FirstNonBlank(params string?[] values) =>
        values.Select(value => value?.Trim() ?? "")
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
        ?? "";

    static string NormalizeScenePurpose(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "time-skip";

        var key = value.Trim().Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        return key switch
        {
            "location-transition" or "location" => "location-transition",
            "scene-reset" or "reset" => "scene-reset",
            _ => "time-skip"
        };
    }

    static string BuildDirectionGuidance(string direction, bool autonomous) => RpCyoaDirections.Normalize(direction) switch
    {
        RpCyoaDirections.Continue => "Continue the current narrative path and add one fresh playable beat.",
        RpCyoaDirections.Escalate => "Escalate the current narrative path by increasing pressure, stakes, intimacy, conflict, urgency, or consequence.",
        RpCyoaDirections.Pivot => autonomous
            ? "Pivot without jumping time or changing scene: redirect topic, focus, tactic, attention, or emotional angle."
            : "Change direction by redirecting topic, focus, tactic, attention, or emotional angle.",
        RpCyoaDirections.FastForward => "Suggest moving time forward, waiting, changing location, or resetting the scene for user approval.",
        _ => "Continue the current narrative path."
    };

    static CyoaActorSelection FallbackCyoaActor(RpChatDocument document, SelectCyoaActorRequest request)
    {
        var scene = TranscriptGraph.GetSceneForNextTurn(document.Transcript, request.ParentTurnId);
        var candidates = document.Characters
            .Where(character => scene.InSceneCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            .Where(character => !request.ForceControlled || request.ControlledCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            .ToList();
        var actor = candidates.FirstOrDefault()
            ?? document.Characters.FirstOrDefault(character => request.ControlledCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            ?? document.Characters.FirstOrDefault();
        return actor is null
            ? new("", "Narrator", true)
            : new(actor.Id, actor.Name, false);
    }

    static RpCharacter? ResolveCharacter(IEnumerable<RpCharacter> characters, string idOrName) =>
        characters.FirstOrDefault(character =>
            string.Equals(character.Id, idOrName, StringComparison.Ordinal)
            || string.Equals(character.Name, idOrName, StringComparison.OrdinalIgnoreCase));

    static RpLocation? ResolveLocation(IEnumerable<RpLocation> locations, string idOrName) =>
        locations.FirstOrDefault(location =>
            string.Equals(location.Id, idOrName, StringComparison.Ordinal)
            || string.Equals(location.Name, idOrName, StringComparison.OrdinalIgnoreCase));

    static List<string> ResolveCharacterIds(IReadOnlyList<RpCharacter> characters, IReadOnlyList<string>? names) =>
        names?.Select(name => ResolveCharacter(characters, name)?.Id ?? "")
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList()
        ?? [];

    static List<string> ResolveItemIds(IReadOnlyList<RpItem> items, IReadOnlyList<string>? names) =>
        names?.Select(name => items.FirstOrDefault(item =>
                string.Equals(item.Id, name, StringComparison.Ordinal)
                || string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))?.Id ?? "")
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList()
        ?? [];

    static string FormatCyoaActorCandidates(IReadOnlyList<RpCharacter> characters, IReadOnlyList<string> controlledIds) =>
        string.Join(Environment.NewLine, characters.Select(character =>
            $"- {character.Name}{(controlledIds.Contains(character.Id, StringComparer.Ordinal) ? " (controlled)" : "")}: {PromptText(character.Summary, "No summary")}"));

    static string FormatCyoaRecentTranscript(RpChatDocument document, string parentTurnId)
    {
        var activePath = TranscriptGraph.GetActivePath(document.Transcript);
        var parentIndex = activePath.FindIndex(turn => string.Equals(turn.Id, parentTurnId, StringComparison.Ordinal));
        if (parentIndex >= 0)
            activePath = activePath.Take(parentIndex + 1).ToList();

        var lines = activePath
            .TakeLast(8)
            .Where(turn => !string.IsNullOrWhiteSpace(turn.Body))
            .Select(turn => $"- {turn.AuthorName}: {PromptText(turn.Body)}")
            .ToList();
        return lines.Count == 0 ? "None" : string.Join(Environment.NewLine, lines);
    }

    static string FormatReferenceNames(IEnumerable<string> names)
    {
        var values = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList();
        return values.Count == 0 ? "None" : string.Join(Environment.NewLine, values.Select(name => $"- {name}"));
    }

    static string PromptText(string? value, string fallback = "Unknown")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim().ReplaceLineEndings(" ");
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
        PrivateIntent = privateIntent,
        ContinuityIntents = plan.ContinuityIntents
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

        if (stepId == PromptLibraryStageIds.SceneContinuity && state.Values.TryGetValue(PromptLibraryStageIds.LegacyAppearance, out tuning))
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
        Guardrails = plan.Guardrails,
        ContinuityIntents = NormalizeContinuityIntents(plan.ContinuityIntents)
    };

    static RpGenerationTraceStep CreateStepTrace(
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

    static void SeedTurnSteps(RpGenerationTrace trace, ActiveModelSelection selection, bool structured, bool includeSelection = true)
    {
        var steps = structured
            ? includeSelection
                ? new[] { ("scene-continuity", "Scene Continuity"), ("selection", "Selection"), ("planning", "Planning"), ("prose", "Prose") }
                : new[] { ("scene-continuity", "Scene Continuity"), ("planning", "Planning"), ("prose", "Prose") }
            : new[] { ("prose", "Prose") };

        SeedSteps(trace, selection, steps);
    }

    static void SeedPlanningAndProseSteps(RpGenerationTrace trace, ActiveModelSelection selection) =>
        SeedSteps(trace, selection, [("planning", "Planning"), ("prose", "Prose")]);

    static void SeedSelectedCyoaTurnSteps(RpGenerationTrace trace, ActiveModelSelection selection) =>
        SeedSteps(trace, selection, [("scene-continuity", "Scene Continuity"), ("planning", "Planning"), ("prose", "Prose")]);

    static void SeedSteps(RpGenerationTrace trace, ActiveModelSelection selection, IReadOnlyList<(string Id, string Label)> steps)
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

    static RpGenerationTrace CreateSelectedCyoaTrace(RpCyoaPendingDecision decision, ActiveModelSelection selection)
    {
        var trace = decision.Trace is null
            ? new()
            {
                StartedUtc = DateTime.UtcNow,
                ProviderId = selection.Provider.Id,
                ProviderName = selection.Provider.Name,
                ProviderType = selection.Provider.Type,
                ModelId = selection.Model.Id
            }
            : SessionCloner.Clone(decision.Trace);
        trace.Status = "running";
        trace.CompletedUtc = default;
        trace.DurationSeconds = 0;
        trace.InputTokens = trace.Steps.Sum(step => step.InputTokens);
        trace.OutputTokens = trace.Steps.Sum(step => step.OutputTokens);
        trace.TotalTokens = trace.Steps.Sum(step => step.TotalTokens);
        if (trace.StartedUtc == default)
            trace.StartedUtc = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(trace.ProviderId))
        {
            trace.ProviderId = selection.Provider.Id;
            trace.ProviderName = selection.Provider.Name;
            trace.ProviderType = selection.Provider.Type;
            trace.ModelId = selection.Model.Id;
        }

        return trace;
    }

    static string ResolveSelectedCyoaGuidance(GenerateSelectedCyoaTurnRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomGuidance))
            return request.CustomGuidance.Trim();

        return request.Option?.Guidance.Trim() ?? "";
    }

    static string BuildSelectedCyoaPlanningGuidance(string guidance, RpCyoaOption? option)
    {
        if (option is null)
            return guidance;

        var plan = option.Plan;
        return $"""
            {guidance}

            Selected choice:
            Title: {option.Title}
            Summary: {option.Summary}
            Direction intent: {BuildDirectionGuidance(option.Direction, autonomous: false)}

            Choice planning seed:
            Beat: {plan.Beat}
            Intent: {plan.Intent}
            Immediate Goal: {plan.ImmediateGoal}
            Why Now: {plan.WhyNow}
            Change Introduced: {plan.ChangeIntroduced}
            Guardrails: {plan.Guardrails}
            """;
    }

    static async Task StartStepAsync(RpGenerationTrace trace, string stepId, ActiveModelSelection selection, DateTime startedUtc, TranscriptGenerationProgress? progress)
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

    static async Task CompleteStepAsync(RpGenerationTrace trace, RpGenerationTraceStep completedStep, TranscriptGenerationProgress? progress)
    {
        var step = FindStep(trace, completedStep.Id);
        CopyStep(completedStep, step);
        trace.Summary = $"Generating · {string.Join(" -> ", trace.Steps.Select(item => item.Label))}";
        await ReportProgressAsync(progress, trace);
    }

    static RpGenerationTraceStep FindStep(RpGenerationTrace trace, string stepId)
    {
        var step = trace.Steps.FirstOrDefault(step => step.Id == stepId);
        if (step is not null)
            return step;

        step = new() { Id = stepId, Label = stepId };
        trace.Steps.Add(step);
        return step;
    }

    static void CopyStep(RpGenerationTraceStep source, RpGenerationTraceStep target)
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

    static void FailRunningStep(RpGenerationTrace trace, string error)
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

    static Task ReportProgressAsync(TranscriptGenerationProgress? progress, RpGenerationTrace trace) =>
        progress?.ReportAsync(trace) ?? Task.CompletedTask;

    static void FinalizeTrace(RpGenerationTrace trace, string status)
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

    static string ResolveCharacterId(IEnumerable<RpCharacter> characters, string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
            return "";

        var match = characters.FirstOrDefault(character => string.Equals(character.Id, idOrName, StringComparison.Ordinal)
            || string.Equals(character.Name, idOrName, StringComparison.OrdinalIgnoreCase));
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
        Physical Continuity Intent: {FormatContinuityIntents(plan.ContinuityIntents)}
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

    static List<RpCharacterPhysicalState> NormalizePhysicalStates(
        IReadOnlyList<RpCharacter> characters,
        IReadOnlyList<SceneContinuityPhysicalStateResponse>? states)
    {
        if (states is null)
            return [];

        return states
            .Select(state =>
            {
                var characterId = ResolveCharacterId(characters, state.CharacterName ?? "");
                if (string.IsNullOrWhiteSpace(characterId))
                    characterId = ResolveCharacterId(characters, state.CharacterId ?? "");
                if (string.IsNullOrWhiteSpace(characterId))
                    characterId = state.CharacterId?.Trim() ?? "";
                return new RpCharacterPhysicalState
                {
                    CharacterId = characterId,
                    Location = state.Location?.Trim() ?? "",
                    Posture = state.Posture?.Trim() ?? "",
                    Head = state.Head?.Trim() ?? "",
                    LeftArm = state.LeftArm?.Trim() ?? "",
                    RightArm = state.RightArm?.Trim() ?? "",
                    LeftHand = state.LeftHand?.Trim() ?? "",
                    RightHand = state.RightHand?.Trim() ?? "",
                    LeftLeg = state.LeftLeg?.Trim() ?? "",
                    RightLeg = state.RightLeg?.Trim() ?? "",
                    LeftFoot = state.LeftFoot?.Trim() ?? "",
                    RightFoot = state.RightFoot?.Trim() ?? "",
                    Contact = state.Contact?.Trim() ?? "",
                    Summary = state.Summary?.Trim() ?? ""
                };
            })
            .Where(state => !string.IsNullOrWhiteSpace(state.CharacterId))
            .GroupBy(state => state.CharacterId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
    }

    static List<RpSceneObjectState> NormalizeSceneObjects(
        IReadOnlyList<RpCharacter> characters,
        IReadOnlyList<SceneContinuityObjectResponse>? objects)
    {
        if (objects is null)
            return [];

        return objects
            .Select((item, index) => new RpSceneObjectState
            {
                Id = CreateSceneObjectId(item, index),
                Name = item.Name?.Trim() ?? "",
                OwnerCharacterId = ResolveObjectCharacterId(characters, item.OwnerCharacterId),
                HolderCharacterId = ResolveObjectCharacterId(characters, item.HolderCharacterId),
                HeldBodyPart = item.HeldBodyPart?.Trim() ?? "",
                Location = item.Location?.Trim() ?? "",
                State = item.State?.Trim() ?? "",
                Summary = item.Summary?.Trim() ?? ""
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Summary))
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
    }

    static string ResolveObjectCharacterId(IEnumerable<RpCharacter> characters, string? idOrName)
    {
        var characterId = ResolveCharacterId(characters, idOrName ?? "");
        return string.IsNullOrWhiteSpace(characterId) ? idOrName?.Trim() ?? "" : characterId;
    }

    static string CreateSceneObjectId(SceneContinuityObjectResponse item, int index)
    {
        if (!string.IsNullOrWhiteSpace(item.Id))
            return item.Id.Trim();

        var source = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.Summary;
        if (string.IsNullOrWhiteSpace(source))
            return $"scene-object-{index + 1}";

        var builder = new StringBuilder("scene-object-");
        var previousDash = false;
        foreach (var character in source.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousDash = false;
            }
            else if (!previousDash && builder.Length > "scene-object-".Length)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        if (previousDash)
            builder.Length--;

        return builder.Length == "scene-object-".Length ? $"scene-object-{index + 1}" : builder.ToString();
    }

    static List<RpPhysicalContinuityIntent> NormalizeContinuityIntents(IReadOnlyList<RpPhysicalContinuityIntent>? intents) =>
        intents?
            .Where(intent => !string.IsNullOrWhiteSpace(intent.Change) || !string.IsNullOrWhiteSpace(intent.Kind))
            .Select(intent => new RpPhysicalContinuityIntent
            {
                Kind = intent.Kind.Trim(),
                CharacterName = intent.CharacterName.Trim(),
                CharacterId = intent.CharacterId.Trim(),
                BodyPart = intent.BodyPart.Trim(),
                ObjectName = intent.ObjectName.Trim(),
                ObjectId = intent.ObjectId.Trim(),
                Target = intent.Target.Trim(),
                Change = intent.Change.Trim(),
                ClearsStaleState = intent.ClearsStaleState
            })
            .ToList()
        ?? [];

    static string FormatContinuityIntents(IReadOnlyList<RpPhysicalContinuityIntent>? intents)
    {
        var values = NormalizeContinuityIntents(intents);
        if (values.Count == 0)
            return "None";

        return string.Join(" | ", values.Select(intent =>
        {
            var parts = new List<string>();
            Add(parts, intent.Kind);
            Add(parts, intent.CharacterName);
            Add(parts, intent.BodyPart);
            Add(parts, intent.ObjectName);
            Add(parts, intent.Target);
            Add(parts, intent.Change);
            if (intent.ClearsStaleState)
                parts.Add("clears stale state");
            return string.Join("; ", parts);
        }));

        static void Add(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add(value.Trim());
        }
    }

    public sealed record AppearanceResponse(
        string? Summary,
        IReadOnlyList<AppearanceCharacterResponse>? Characters,
        IReadOnlyList<SceneContinuityPhysicalStateResponse>? PhysicalStates = null,
        IReadOnlyList<SceneContinuityObjectResponse>? SceneObjects = null);

    public sealed record AppearanceCharacterResponse(
        string CharacterName,
        bool HasCurrentSceneState,
        string? CurrentAppearance);

    public sealed record SceneContinuityPhysicalStateResponse(
        string? CharacterId,
        string? CharacterName,
        string? Location,
        string? Posture,
        string? Head,
        string? LeftArm,
        string? RightArm,
        string? LeftHand,
        string? RightHand,
        string? LeftLeg,
        string? RightLeg,
        string? LeftFoot,
        string? RightFoot,
        string? Contact,
        string? Summary);

    public sealed record SceneContinuityObjectResponse(
        string? Id,
        string? Name,
        string? OwnerCharacterId,
        string? HolderCharacterId,
        string? HeldBodyPart,
        string? Location,
        string? State,
        string? Summary);

    sealed record SceneContinuityResult(
        Dictionary<string, string> CharacterSceneStates,
        RpSceneFrame Scene);

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
        public IReadOnlyList<RpPhysicalContinuityIntent>? ContinuityIntents { get; set; }
    }

    sealed class CyoaActorSelectionResponse
    {
        public string CharacterName { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    sealed class CyoaDirectionChoiceResponse
    {
        public string Direction { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    sealed class CyoaDecisionResponse
    {
        public IReadOnlyList<CyoaOptionResponse> Options { get; set; } = [];
    }

    sealed class CyoaOptionResponse
    {
        public string Direction { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Guidance { get; set; } = "";
        public string ActorName { get; set; } = "";
        public string TurnShape { get; set; } = "";
        public string Beat { get; set; } = "";
        public string Intent { get; set; } = "";
        public string ImmediateGoal { get; set; } = "";
        public string WhyNow { get; set; } = "";
        public string ChangeIntroduced { get; set; } = "";
        public string Guardrails { get; set; } = "";
        public string PrivateIntent { get; set; } = "";
        public IReadOnlyList<RpPhysicalContinuityIntent>? ContinuityIntents { get; set; }
        public string LocationName { get; set; } = "";
        public IReadOnlyList<string>? CharacterNames { get; set; }
        public IReadOnlyList<string>? ItemNames { get; set; }
        public string ScenePurpose { get; set; } = "";
        public string SceneGuidance { get; set; } = "";
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

    static List<RpTranscriptSnapshotRelationshipUpdate> NormalizeSnapshotRelationshipUpdates(
        IReadOnlyList<SnapshotRelationshipUpdateResponse>? updates,
        RpChatDocument document,
        CharacterTraitLibraryState traitLibrary)
    {
        if (updates is null || updates.Count == 0)
            return [];

        var relationshipsById = document.CharacterRelationships.ToDictionary(relationship => relationship.Id, StringComparer.Ordinal);
        var relationshipTypes = ControlledValueMap(traitLibrary.BondTypes);
        var privateTensions = ControlledValueMap(traitLibrary.Dynamics);
        return updates
            .Select(update => NormalizeSnapshotRelationshipUpdate(update, relationshipsById, relationshipTypes, privateTensions))
            .Where(update => update is not null)
            .Select(update => update!)
            .GroupBy(update => update.RelationshipId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToList();
    }

    static RpTranscriptSnapshotRelationshipUpdate? NormalizeSnapshotRelationshipUpdate(
        SnapshotRelationshipUpdateResponse update,
        IReadOnlyDictionary<string, RpCharacterRelationship> relationshipsById,
        IReadOnlyDictionary<string, string> relationshipTypes,
        IReadOnlyDictionary<string, string> privateTensions)
    {
        var relationshipId = update.RelationshipId.Trim();
        if (!relationshipsById.TryGetValue(relationshipId, out var relationship))
            return null;
        if (!string.Equals(update.SourceCharacterId.Trim(), relationship.CharacterAId, StringComparison.Ordinal)
            || !string.Equals(update.TargetCharacterId.Trim(), relationship.CharacterBId, StringComparison.Ordinal))
            return null;

        var normalizedRelationshipTypes = NormalizeControlledValues(update.RelationshipTypes, relationshipTypes);
        var normalizedPrivateTensions = NormalizeControlledValues(update.PrivateTensions, privateTensions);
        var howSourceSeesTarget = update.HowSourceSeesTarget.Trim();
        var howTargetSeesSource = update.HowTargetSeesSource.Trim();
        var publicDynamic = update.PublicDynamic.Trim();
        if (normalizedRelationshipTypes.Count == 0
            || normalizedPrivateTensions.Count == 0
            || string.IsNullOrWhiteSpace(howSourceSeesTarget)
            || string.IsNullOrWhiteSpace(howTargetSeesSource)
            || string.IsNullOrWhiteSpace(publicDynamic))
            return null;
        if (SameValues(relationship.Bonds, normalizedRelationshipTypes)
            && SameValues(relationship.Dynamics, normalizedPrivateTensions)
            && string.Equals(relationship.NoteAtoB.Trim(), howSourceSeesTarget, StringComparison.Ordinal)
            && string.Equals(relationship.NoteBtoA.Trim(), howTargetSeesSource, StringComparison.Ordinal)
            && string.Equals(relationship.NoteExternal.Trim(), publicDynamic, StringComparison.Ordinal))
            return null;

        return new()
        {
            RelationshipId = relationshipId,
            SourceCharacterId = relationship.CharacterAId,
            TargetCharacterId = relationship.CharacterBId,
            RelationshipTypes = normalizedRelationshipTypes,
            PrivateTensions = normalizedPrivateTensions,
            HowSourceSeesTarget = howSourceSeesTarget,
            HowTargetSeesSource = howTargetSeesSource,
            PublicDynamic = publicDynamic,
            Reason = update.Reason.Trim(),
            EvidenceTurnNumbers = update.EvidenceTurnNumbers?
                .Where(turnNumber => turnNumber > 0)
                .Distinct()
                .OrderBy(turnNumber => turnNumber)
                .ToList()
                ?? []
        };
    }

    static Dictionary<string, string> ControlledValueMap(IEnumerable<string> values)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values.Select(value => value.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)))
            map.TryAdd(value, value);

        return map;
    }

    static List<string> NormalizeControlledValues(IReadOnlyList<string>? values, IReadOnlyDictionary<string, string> controlledValues) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => controlledValues.TryGetValue(value.Trim(), out var controlledValue) ? controlledValue : "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    static bool SameValues(IReadOnlyList<string> current, IReadOnlyList<string> updated) =>
        current.Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(updated.Where(value => !string.IsNullOrWhiteSpace(value)));

    public sealed class SnapshotTimelineEntryResponse
    {
        public int TurnNumber { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public IReadOnlyList<string>? CharacterNames { get; set; }
        public IReadOnlyList<string>? LocationNames { get; set; }
        public IReadOnlyList<string>? ItemNames { get; set; }
    }

    public sealed class SnapshotRelationshipUpdateResponse
    {
        public string RelationshipId { get; set; } = "";
        public string SourceCharacterId { get; set; } = "";
        public string TargetCharacterId { get; set; } = "";
        public IReadOnlyList<string>? RelationshipTypes { get; set; }
        public IReadOnlyList<string>? PrivateTensions { get; set; }
        public string HowSourceSeesTarget { get; set; } = "";
        public string HowTargetSeesSource { get; set; } = "";
        public string PublicDynamic { get; set; } = "";
        public string Reason { get; set; } = "";
        public IReadOnlyList<int>? EvidenceTurnNumbers { get; set; }
    }

    sealed class SnapshotResponse
    {
        public string NarrativeSummary { get; set; } = "";
        public string Summary { get; set; } = "";
        public IReadOnlyList<SnapshotTimelineEntryResponse>? TimelineEntries { get; set; }
        public IReadOnlyList<SnapshotRelationshipUpdateResponse>? RelationshipUpdates { get; set; }
    }
}
