using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

enum CyoaDecisionInvalidationReason
{
    ModeChanged,
    ControlledCastChanged,
    ChoiceConsumed,
    ChoiceSkipped,
    BranchChanged,
    TurnDeleted,
    SceneChanged,
    RecoveryStarted,
    FastForwardApplied
}

public sealed partial class TranscriptStore
{
    public RpCyoaPendingDecision? CurrentCyoaDecision =>
        Document is null || !IsCyoaDecisionCurrent(Document.Transcript.Cyoa.PendingDecision)
            ? null
            : Document.Transcript.Cyoa.PendingDecision;

    public bool NeedsCyoaDecisionRecovery =>
        Document is not null
        && !IsBusy
        && RpCyoaModes.IsActive(Document.Transcript.Cyoa.Mode)
        && CurrentCyoaDecision is null;

    public async Task SetCyoaModeAsync(string mode) => await RunExclusiveAsync("Updating mode...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var normalized = NormalizeCyoaMode(mode);
        Document.Transcript.Cyoa.Mode = normalized;
        InvalidateCyoaDecision(CyoaDecisionInvalidationReason.ModeChanged);
        Document.Transcript.Cyoa.AutoplayRemaining = RpCyoaState.MaxAutoplayTurns;
        if (normalized == RpCyoaModes.Adventure)
            EnsureControlledCharacters();

        await SaveTranscriptAsync();
        await ContinueCyoaPipelineAsync();
    });

    public async Task ToggleCyoaControlledCharacterAsync(string characterId) => await RunExclusiveAsync("Updating cast...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var controlled = Document.Transcript.Cyoa.ControlledCharacterIds;
        if (controlled.Contains(characterId, StringComparer.Ordinal))
            controlled.RemoveAll(id => string.Equals(id, characterId, StringComparison.Ordinal));
        else if (Document.Characters.Any(character => string.Equals(character.Id, characterId, StringComparison.Ordinal)))
            controlled.Add(characterId);

        Document.Transcript.Cyoa.ControlledCharacterIds = controlled.Distinct(StringComparer.Ordinal).ToList();
        InvalidateCyoaDecision(CyoaDecisionInvalidationReason.ControlledCastChanged);
        Document.Transcript.Cyoa.AutoplayRemaining = RpCyoaState.MaxAutoplayTurns;
        if (Document.Transcript.Cyoa.Mode == RpCyoaModes.Adventure && Document.Transcript.Cyoa.ControlledCharacterIds.Count == 0)
            Document.Transcript.Cyoa.Mode = RpCyoaModes.Off;

        await SaveTranscriptAsync();
        await ContinueCyoaPipelineAsync();
    });

    public async Task SelectCyoaOptionAsync(string optionId) => await RunExclusiveAsync("Writing choice...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var decision = CurrentCyoaDecision;
        var option = decision?.Options.FirstOrDefault(item => string.Equals(item.Id, optionId, StringComparison.Ordinal));
        if (decision is null || option is null)
            return;

        if (option.Direction == RpCyoaDirections.FastForward && option.SceneProposal is not null)
        {
            decision.FastForwardReview = new() { OptionId = option.Id, Proposal = SessionCloner.Clone(option.SceneProposal) };
            await SaveTranscriptAsync();
            return;
        }

        InvalidateCyoaDecision(CyoaDecisionInvalidationReason.ChoiceConsumed);
        var turn = await GenerateSelectedCyoaTurnCoreAsync(new(decision, option, ""));
        if (turn is not null)
            await ContinueCyoaPipelineAsync();
    });

    public async Task SubmitCyoaCustomGuidanceAsync(string guidance) => await RunExclusiveAsync("Writing choice...", async () =>
    {
        if (Document is null || string.IsNullOrWhiteSpace(guidance))
            return;

        ClearBackgroundError();
        var decision = CurrentCyoaDecision;
        if (decision is null)
            return;

        InvalidateCyoaDecision(CyoaDecisionInvalidationReason.ChoiceConsumed);
        var turn = await GenerateSelectedCyoaTurnCoreAsync(new(decision, null, guidance.Trim()));
        if (turn is not null)
            await ContinueCyoaPipelineAsync();
    });

    public async Task SkipCyoaDecisionAsync() => await RunExclusiveAsync("Skipping...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        InvalidateCyoaDecision(CyoaDecisionInvalidationReason.ChoiceSkipped);
        Document.Transcript.Cyoa.AutoplayRemaining = RpCyoaState.MaxAutoplayTurns;
        await SaveTranscriptAsync();
        await ContinueCyoaPipelineAsync();
    });

    public async Task RecoverCyoaDecisionAsync() => await RunExclusiveAsync("Restoring options...", async () =>
    {
        if (Document is null || !RpCyoaModes.IsActive(Document.Transcript.Cyoa.Mode))
            return;

        ClearBackgroundError();
        InvalidateCyoaDecision(CyoaDecisionInvalidationReason.RecoveryStarted);
        Document.Transcript.Cyoa.AutoplayRemaining = RpCyoaState.MaxAutoplayTurns;
        await SaveTranscriptAsync();
        if (Document.Transcript.Cyoa.Mode == RpCyoaModes.Director)
            await GenerateDirectorDecisionAsync();
        else if (Document.Transcript.Cyoa.Mode == RpCyoaModes.Adventure)
            await GenerateAdventureRecoveryDecisionAsync();
    });

    public async Task UpdateCyoaFastForwardGuidanceAsync(string guidance) => await RunExclusiveAsync("Updating scene proposal...", async () =>
    {
        var document = Document;
        if (document?.Transcript.Cyoa.PendingDecision?.FastForwardReview is null)
            return;

        var proposal = document.Transcript.Cyoa.PendingDecision.FastForwardReview.Proposal;
        proposal.Guidance = string.IsNullOrWhiteSpace(guidance)
            ? NormalizeCyoaSceneGuidance(proposal.Guidance)
            : guidance.Trim();
        await SaveTranscriptAsync();
    });

    public async Task ApplyCyoaFastForwardAsync() => await RunExclusiveAsync("Setting scene...", async () =>
    {
        var document = Document;
        if (document?.Transcript.Cyoa.PendingDecision?.FastForwardReview is null)
            return;

        ClearBackgroundError();
        var decision = document.Transcript.Cyoa.PendingDecision;
        var proposal = decision.FastForwardReview.Proposal;
        try
        {
            var result = await SetSceneCoreAsync(BuildSetSceneRequest(proposal));
            if (result.Turn?.Trace is not null)
                result.Turn.Trace = PrependCyoaChoicesTrace(decision.Trace, result.Turn.Trace);

            InvalidateCyoaDecision(CyoaDecisionInvalidationReason.FastForwardApplied);
            await SaveTranscriptAsync();
            if (result.Turn is not null)
                await ContinueCyoaPipelineAsync();
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception, logger, "Applying CYOA fast forward failed unexpectedly.");
            await SaveTranscriptAsync();
        }
    });

    public async Task CancelCyoaFastForwardAsync() => await RunExclusiveAsync("Canceling scene proposal...", async () =>
    {
        var document = Document;
        if (document?.Transcript.Cyoa.PendingDecision?.FastForwardReview is null)
            return;

        document.Transcript.Cyoa.PendingDecision.FastForwardReview = null;
        await SaveTranscriptAsync();
    });

    async Task ContinueCyoaPipelineAsync()
    {
        if (Document is null)
            return;

        if (CurrentCyoaDecision is not null)
            return;

        if (Document.Transcript.Cyoa.PendingDecision is not null)
        {
            InvalidateCyoaDecision(CyoaDecisionInvalidationReason.BranchChanged);
            await SaveTranscriptAsync();
        }

        if (Document.Transcript.Cyoa.Mode == RpCyoaModes.Director)
        {
            await GenerateDirectorDecisionAsync();
            return;
        }

        if (Document.Transcript.Cyoa.Mode == RpCyoaModes.Adventure)
            await ContinueAdventurePipelineAsync();
    }

    async Task ContinueAdventurePipelineAsync()
    {
        if (Document is null)
            return;

        var controlledIds = NormalizeControlledCharacters();
        if (controlledIds.Count == 0)
        {
            Document.Transcript.Cyoa.Mode = RpCyoaModes.Off;
            await SaveTranscriptAsync();
            return;
        }

        while (Document.Transcript.Cyoa.Mode == RpCyoaModes.Adventure
            && CurrentCyoaDecision is null)
        {
            var forceControlled = Document.Transcript.Cyoa.AutoplayRemaining <= 0;
            var actor = await SelectCyoaActorAsync(forceControlled);
            if (forceControlled || controlledIds.Contains(actor.ActorCharacterId, StringComparer.Ordinal))
            {
                Document.Transcript.Cyoa.AutoplayRemaining = RpCyoaState.MaxAutoplayTurns;
                await GenerateAdventureDecisionAsync(actor);
                return;
            }

            var turn = await GenerateAutonomousCyoaTurnCoreAsync(actor);
            if (turn is null)
                return;

            Document.Transcript.Cyoa.AutoplayRemaining--;
        }
    }

    async Task GenerateAdventureRecoveryDecisionAsync()
    {
        if (Document is null)
            return;

        EnsureControlledCharacters();
        if (Document.Transcript.Cyoa.ControlledCharacterIds.Count == 0)
        {
            await SaveTranscriptAsync();
            return;
        }

        var actor = await SelectCyoaActorAsync(true);
        Document.Transcript.Cyoa.AutoplayRemaining = RpCyoaState.MaxAutoplayTurns;
        await GenerateAdventureDecisionAsync(actor);
    }

    async Task<CyoaActorSelection> SelectCyoaActorAsync(bool forceControlled)
    {
        if (Document is null)
            return new("", "Narrator", true);

        try
        {
            return await textGenerationService.SelectCyoaActorAsync(
                Document,
                providers.Items.ToList(),
                RuntimeConfig,
                new(
                    Document.Transcript.ActiveLeafTurnId,
                    Document.Transcript.Cyoa.ControlledCharacterIds,
                    forceControlled));
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception, logger, "Selecting CYOA actor failed unexpectedly.");
            return FallbackCyoaActor(forceControlled);
        }
    }

    CyoaActorSelection FallbackCyoaActor(bool forceControlled)
    {
        if (Document is null)
            return new("", "Narrator", true);

        var scene = TranscriptGraph.GetSceneForNextTurn(Document.Transcript, Document.Transcript.ActiveLeafTurnId);
        var candidates = Document.Characters
            .Where(character => scene.InSceneCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            .Where(character => !forceControlled || Document.Transcript.Cyoa.ControlledCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            .ToList();
        var actor = candidates.FirstOrDefault()
            ?? Document.Characters.FirstOrDefault(character => Document.Transcript.Cyoa.ControlledCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            ?? Document.Characters.FirstOrDefault();
        return actor is null ? new("", "Narrator", true) : new(actor.Id, actor.Name, false);
    }

    async Task GenerateAdventureDecisionAsync(CyoaActorSelection actor)
    {
        if (Document is null)
            return;

        await GenerateCyoaDecisionCoreAsync(new(
            Document.Transcript.ActiveLeafTurnId,
            RpCyoaModes.Adventure,
            actor.ActorCharacterId,
            actor.ActorName,
            actor.RequestedNarrator));
    }

    async Task GenerateDirectorDecisionAsync()
    {
        if (Document is null)
            return;

        await GenerateCyoaDecisionCoreAsync(new(
            Document.Transcript.ActiveLeafTurnId,
            RpCyoaModes.Director,
            "",
            "Narrator",
            true));
    }

    async Task GenerateCyoaDecisionCoreAsync(GenerateCyoaDecisionRequest request)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateCyoaDecisionAsync(
                Document,
                providers.Items.ToList(),
                RuntimeConfig,
                request,
                new(UpdateActiveTraceAsync));
            Document.Transcript.Cyoa.PendingDecision = result.Decision;
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
        }
        catch (TranscriptGenerationException exception)
        {
            CaptureBackgroundError(exception, logger, "Generating CYOA choices failed with a model trace.");
            await ClearActiveTraceAsync();
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception, logger, "Generating CYOA choices failed unexpectedly.");
            await ClearActiveTraceAsync();
        }
    }

    async Task<RpTranscriptTurn?> GenerateAutonomousCyoaTurnCoreAsync(CyoaActorSelection actor)
    {
        if (Document is null)
            return null;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateAutonomousCyoaTurnAsync(
                Document,
                providers.Items.ToList(),
                RuntimeConfig,
                new(
                    Document.Transcript.ActiveLeafTurnId,
                    "automatic",
                    actor.ActorCharacterId,
                    actor.ActorName,
                    actor.RequestedNarrator),
                new(UpdateActiveTraceAsync, UpdateActiveDraftAsync));
            return await CommitGeneratedTurnAsync(Document.Transcript.ActiveLeafTurnId, "", "automatic", result);
        }
        catch (TranscriptGenerationException exception)
        {
            ClearActiveDraft();
            PersistFailedTurn(
                Document.Transcript.ActiveLeafTurnId,
                "",
                actor.RequestedNarrator ? null : new() { Id = actor.ActorCharacterId, Name = actor.ActorName },
                actor.RequestedNarrator,
                "automatic",
                TurnShapeRules.AutoLabel,
                exception.Trace);
            CaptureBackgroundError(exception, logger, "Generating autonomous CYOA turn failed with a model trace.");
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
        catch (Exception exception)
        {
            ClearActiveDraft();
            CaptureBackgroundError(exception, logger, "Generating autonomous CYOA turn failed unexpectedly.");
            await ClearActiveTraceAsync();
            return null;
        }
    }

    async Task<RpTranscriptTurn?> GenerateSelectedCyoaTurnCoreAsync(GenerateSelectedCyoaTurnRequest request)
    {
        if (Document is null)
            return null;

        ClearBackgroundError();
        var option = request.Option;
        var requestedNarrator = option?.RequestedNarrator ?? request.Decision.RequestedNarrator;
        var actorId = requestedNarrator ? "" : option?.ActorCharacterId ?? request.Decision.ActorCharacterId;
        var actorName = requestedNarrator ? "Narrator" : option?.ActorName ?? request.Decision.ActorName;
        var guidance = !string.IsNullOrWhiteSpace(request.CustomGuidance) ? request.CustomGuidance.Trim() : option?.Guidance.Trim() ?? "";
        var turnShape = string.IsNullOrWhiteSpace(option?.Plan.TurnShape) ? TurnShapeRules.AutoLabel : option.Plan.TurnShape;
        var actor = requestedNarrator ? null : new RpCharacter { Id = actorId, Name = actorName };
        try
        {
            var result = await textGenerationService.GenerateSelectedCyoaTurnAsync(
                Document,
                providers.Items.ToList(),
                RuntimeConfig,
                request,
                new(UpdateActiveTraceAsync, UpdateActiveDraftAsync));
            return await CommitGeneratedTurnAsync(request.Decision.ParentTurnId, guidance, "guided", result);
        }
        catch (TranscriptGenerationException exception)
        {
            ClearActiveDraft();
            PersistFailedTurn(
                request.Decision.ParentTurnId,
                guidance,
                actor,
                requestedNarrator,
                "guided",
                turnShape,
                exception.Trace,
                scene: option?.Scene);
            CaptureBackgroundError(exception, logger, "Generating selected CYOA turn failed with a model trace.");
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
        catch (Exception exception)
        {
            ClearActiveDraft();
            CaptureBackgroundError(exception, logger, "Generating selected CYOA turn failed unexpectedly.");
            PersistFailedTurn(
                request.Decision.ParentTurnId,
                guidance,
                actor,
                requestedNarrator,
                "guided",
                turnShape,
                BuildUnhandledFailureTrace(exception),
                scene: option?.Scene);
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
    }

    void InvalidateCyoaDecision(CyoaDecisionInvalidationReason reason)
    {
        if (Document is null)
            return;

        Document.Transcript.Cyoa.PendingDecision = null;
    }

    bool IsCyoaDecisionCurrent(RpCyoaPendingDecision? decision)
    {
        if (Document is null || decision is null)
            return false;

        if (!RpCyoaModes.IsActive(Document.Transcript.Cyoa.Mode))
            return false;
        if (!string.Equals(decision.Mode, Document.Transcript.Cyoa.Mode, StringComparison.Ordinal))
            return false;
        if (!string.Equals(decision.ParentTurnId, Document.Transcript.ActiveLeafTurnId, StringComparison.Ordinal))
            return false;

        return string.IsNullOrWhiteSpace(decision.ParentTurnId)
            || TranscriptGraph.FindTurn(Document.Transcript, decision.ParentTurnId) is not null;
    }

    List<string> NormalizeControlledCharacters()
    {
        if (Document is null)
            return [];

        var controlledIds = Document.Transcript.Cyoa.ControlledCharacterIds
            .Where(id => Document.Characters.Any(character => string.Equals(character.Id, id, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Document.Transcript.Cyoa.ControlledCharacterIds = controlledIds;
        return controlledIds;
    }

    void EnsureControlledCharacters()
    {
        if (Document is null)
            return;

        var controlledIds = NormalizeControlledCharacters();
        if (controlledIds.Count > 0)
            return;

        var scene = TranscriptGraph.GetSceneForNextTurn(Document.Transcript, Document.Transcript.ActiveLeafTurnId);
        var defaultCharacter = Document.Characters.FirstOrDefault(character => scene.InSceneCharacterIds.Contains(character.Id, StringComparer.Ordinal))
            ?? Document.Characters.FirstOrDefault();
        if (defaultCharacter is not null)
            Document.Transcript.Cyoa.ControlledCharacterIds.Add(defaultCharacter.Id);
    }

    static SetSceneRequest BuildSetSceneRequest(RpCyoaSceneProposal proposal) => new(
        proposal.LocationId,
        proposal.CharacterIds,
        proposal.ItemIds,
        new(ScenePurpose(proposal.Purpose), NormalizeCyoaSceneGuidance(proposal.Guidance)));

    static string NormalizeCyoaSceneGuidance(string guidance) =>
        string.IsNullOrWhiteSpace(guidance)
            ? "Move the scene forward in time while preserving established continuity."
            : guidance.Trim();

    static SceneNarratorGuidancePurpose ScenePurpose(string purpose) => purpose switch
    {
        "location-transition" => SceneNarratorGuidancePurpose.LocationTransition,
        "scene-reset" => SceneNarratorGuidancePurpose.SceneReset,
        _ => SceneNarratorGuidancePurpose.TimeSkip
    };

    static RpGenerationTrace PrependCyoaChoicesTrace(RpGenerationTrace? choicesTrace, RpGenerationTrace generatedTrace)
    {
        var merged = SessionCloner.Clone(generatedTrace);
        if (choicesTrace is null || choicesTrace.Steps.Count == 0)
            return merged;

        var choices = SessionCloner.Clone(choicesTrace);
        merged.Steps = [.. choices.Steps, .. merged.Steps];
        if (choices.StartedUtc != default)
            merged.StartedUtc = choices.StartedUtc;
        merged.InputTokens = merged.Steps.Sum(step => step.InputTokens);
        merged.OutputTokens = merged.Steps.Sum(step => step.OutputTokens);
        merged.TotalTokens = merged.Steps.Sum(step => step.TotalTokens);
        if (merged.StartedUtc != default && merged.CompletedUtc != default)
            merged.DurationSeconds = (merged.CompletedUtc - merged.StartedUtc).TotalSeconds;

        var actor = merged.Data["actorName"]?.GetValue<string>() ?? "Narrator";
        var status = string.IsNullOrWhiteSpace(merged.Status) ? "completed" : merged.Status;
        merged.Summary = $"{status[..1].ToUpperInvariant()}{status[1..]} - {actor} - {string.Join(" -> ", merged.Steps.Select(step => step.Label))}";
        return merged;
    }

    static string NormalizeCyoaMode(string mode) => mode switch
    {
        RpCyoaModes.Adventure => RpCyoaModes.Adventure,
        RpCyoaModes.Director => RpCyoaModes.Director,
        _ => RpCyoaModes.Off
    };
}
