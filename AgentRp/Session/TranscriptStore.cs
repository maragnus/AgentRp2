using AgentRp.Models;
using AgentRp.Services;
using Microsoft.Extensions.Logging;

namespace AgentRp.Session;

public sealed partial class TranscriptStore(
    ActiveChatContext activeChat,
    ChatRegistry registry,
    ProviderStore providers,
    ModelSelectionStore modelSelection,
    ITextGenerationService textGenerationService,
    SceneTransitionService sceneTransitionService,
    IMessageSpeechService? messageSpeechService = null,
    ILogger<TranscriptStore>? logger = null) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Transcript;

    public RpTranscriptState State => Document?.Transcript ?? new();
    public RpTranscriptOptionsState Options => State.Options;
    public List<RpTranscriptTurn> Items => Document is null ? [] : TranscriptGraph.GetActivePath(Document.Transcript);
    public RpTranscriptTurn? ActiveLeaf => Document is null ? null : TranscriptGraph.FindTurn(Document.Transcript, Document.Transcript.ActiveLeafTurnId);
    public bool IsBusy { get; private set; }
    public string BusyMessage { get; private set; } = "";
    public RpTurnTrace? ActiveTrace { get; private set; }
    public RpTranscriptTurn? ActiveDraftTurn { get; private set; }

    readonly object _operationLock = new();

    public RpTranscriptSnapshot? SnapshotFor(string turnId) =>
        Document is null ? null : TranscriptGraph.FindSnapshotByTurn(Document.Transcript, turnId);

    public IReadOnlyList<RpTranscriptTurn> SiblingsFor(string turnId) =>
        Document is null ? [] : TranscriptGraph.GetSiblings(Document.Transcript, turnId);

    public async Task SetInjectAudioTagsAsync(bool value) => await SetOptionAsync(options => options.InjectAudioTags = value);

    public async Task SetHideAudioTagsAsync(bool value) => await SetOptionAsync(options => options.HideAudioTags = value);

    public async Task SetShowAppearanceBlocksAsync(bool value) => await SetOptionAsync(options => options.ShowAppearanceBlocks = value);

    public async Task SetShowSceneContinuityBlocksAsync(bool value) => await SetOptionAsync(options => options.ShowSceneContinuityBlocks = value);

    public async Task SetShowProcessTracesAsync(bool value) => await SetOptionAsync(options => options.ShowProcessTraces = value);

    public async Task SetAutoSpeakNewMessagesAsync(bool value) => await SetOptionAsync(options => options.AutoSpeakNewMessages = value);

    public async Task SetSpeakActionsInNarratorVoiceAsync(bool value) => await SetOptionAsync(options => options.SpeakActionsInNarratorVoice = value);

    public async Task SetTurnShapeAsync(string value) => await SetOptionAsync(options => options.TurnShape = TurnShapeRules.NormalizeLabel(value));

    public async Task SetTurnShapeLockedAsync(bool value) => await SetOptionAsync(options => options.TurnShapeLocked = value);

    public async Task<MessageSpeechPlayback?> GetOrGenerateSpeechAsync(string turnId, bool regenerate, CancellationToken cancellationToken = default)
    {
        MessageSpeechPlayback? playback = null;
        await RunExclusiveAsync(regenerate ? "Regenerating speech..." : "Generating speech...", async () =>
        {
            if (Document is null || messageSpeechService is null)
                return;

            ClearBackgroundError();
            var turn = TranscriptGraph.FindTurn(Document.Transcript, turnId);
            if (turn is null)
                return;

            playback = await messageSpeechService.GetOrGenerateAsync(
                Document,
                providers.Items.ToList(),
                modelSelection.State,
                turn,
                regenerate,
                cancellationToken);
            await SaveTranscriptAsync();
        });

        return playback;
    }

    public async Task PostManualAsync(string text, RpCharacter? speaker) => await RunExclusiveAsync("Posting...", async () =>
    {
        if (Document is null || string.IsNullOrWhiteSpace(text))
            return;

        ClearBackgroundError();
        var now = DateTime.UtcNow;
        var authorName = speaker?.Name ?? "Narrator";
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = Document.Transcript.ActiveLeafTurnId,
            TurnNumber = NextTurnNumber(Document.Transcript.ActiveLeafTurnId),
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = "manual",
            AuthorCharacterId = speaker?.Id ?? "",
            AuthorName = authorName,
            ActorCharacterId = speaker?.Id ?? "",
            ActorName = authorName,
            Body = text.Trim(),
            Scene = SessionCloner.Clone(TranscriptGraph.GetSceneForNextTurn(Document.Transcript, Document.Transcript.ActiveLeafTurnId))
        };
        CommitTurn(turn, now);
        await SaveTranscriptAsync();
    });

    public async Task GenerateAsync(string guidance, RpCharacter? requestedActor, bool requestedNarrator, string mode, string turnShape) => await RunExclusiveAsync("Generating...", async () =>
    {
        if (Document is null)
            return;

        await GenerateTurnCoreAsync(
            parentTurnId: Document.Transcript.ActiveLeafTurnId,
            guidance,
            requestedActor,
            requestedNarrator,
            turnShape,
            mode);
    });

    public async Task<SceneTransitionResult?> SetSceneAsync(SetSceneRequest request, CancellationToken cancellationToken = default)
    {
        SceneTransitionPlan? transition = null;
        RpTranscriptTurn? narratorTurn = null;
        await RunExclusiveAsync("Setting scene...", async () =>
        {
            if (Document is null)
                return;

            transition = sceneTransitionService.Build(Document, request);
            narratorTurn = await GenerateTurnCoreAsync(
                parentTurnId: Document.Transcript.ActiveLeafTurnId,
                guidance: transition.NarratorInstruction,
                requestedActor: null,
                requestedNarrator: true,
                turnShape: "Brief",
                mode: "scene-transition",
                sceneOverride: transition.TargetScene);
            if (narratorTurn is null)
                throw new InvalidOperationException("Setting the scene failed while generating the narrator transition.", LastBackgroundError);
        });

        return transition is null || narratorTurn is null
            ? null
            : new(transition, narratorTurn.Id, narratorTurn.Body);
    }

    public async Task RegenerateAsync(string turnId, string guidance, RpCharacter? requestedActor, string turnShape) => await RunExclusiveAsync("Regenerating...", async () =>
    {
        if (Document is null)
            return;

        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var requestedNarrator = requestedActor is null && string.IsNullOrWhiteSpace(original.ActorCharacterId);
        var actor = ResolveBranchActor(original, requestedActor, requestedNarrator);
        var plan = BuildRegenerationPlan(original.Plan, turnShape);
        SelectRegenerationParent(original.ParentTurnId);
        await NotifyChangedAsync();
        await GenerateProseFromPlanCoreAsync(
            BuildProseFromPlanRequest(
                original,
                string.IsNullOrWhiteSpace(guidance) ? original.Guidance : guidance,
                actor,
                requestedNarrator,
                plan));
    });

    public async Task ReplanAsync(string turnId, string guidance, RpCharacter? requestedActor, string turnShape) => await RunExclusiveAsync("Planning new branch...", async () =>
    {
        if (Document is null)
            return;

        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var requestedNarrator = requestedActor is null && string.IsNullOrWhiteSpace(original.ActorCharacterId);
        var actor = ResolveBranchActor(original, requestedActor, requestedNarrator);
        var requestedTurnShape = string.IsNullOrWhiteSpace(turnShape) ? original.Plan.TurnShape : turnShape;
        SelectRegenerationParent(original.ParentTurnId);
        await NotifyChangedAsync();
        await GeneratePlanAndProseCoreAsync(new(
            original.ParentTurnId,
            "replanned",
            guidance,
            requestedTurnShape,
            actor?.Id ?? "",
            requestedNarrator ? "Narrator" : actor?.Name ?? original.ActorName,
            requestedNarrator,
            original.AppearanceByCharacterId,
            original.Scene));
    });

    RpCharacter? ResolveBranchActor(RpTranscriptTurn original, RpCharacter? requestedActor, bool requestedNarrator)
    {
        if (Document is null || requestedNarrator)
            return null;

        return Document.Characters.FirstOrDefault(character => character.Id == original.ActorCharacterId) ?? requestedActor;
    }

    static RpTurnPlan BuildRegenerationPlan(RpTurnPlan source, string turnShape)
    {
        var plan = SessionCloner.Clone(source);
        if (!string.IsNullOrWhiteSpace(turnShape))
            plan.TurnShape = turnShape;

        return plan;
    }

    static GenerateProseFromPlanRequest BuildProseFromPlanRequest(
        RpTranscriptTurn original,
        string guidance,
        RpCharacter? actor,
        bool requestedNarrator,
        RpTurnPlan plan) => new(
            original.ParentTurnId,
            "regenerated",
            string.IsNullOrWhiteSpace(guidance) ? original.Guidance : guidance,
            actor?.Id ?? "",
            requestedNarrator ? "Narrator" : actor?.Name ?? original.ActorName,
            requestedNarrator,
            plan,
            original.AppearanceByCharacterId,
            original.PrivateIntentByCharacterId,
            original.Scene);

    public async Task EditTurnAsync(
        string turnId,
        string body,
        RpTurnPlan? plan = null,
        IReadOnlyDictionary<string, string>? appearances = null,
        IReadOnlyDictionary<string, string>? privateIntents = null) => await RunExclusiveAsync("Saving edit...", async () =>
    {
        if (Document is null || string.IsNullOrWhiteSpace(body))
            return;

        ClearBackgroundError();
        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var trimmedBody = body.Trim();
        var now = DateTime.UtcNow;
        if (!string.Equals(original.Body, trimmedBody, StringComparison.Ordinal))
            await DiscardSpeechAsync(original);

        original.UpdatedUtc = now;
        original.Mode = "edited";
        original.Body = trimmedBody;
        original.Plan = plan is null ? SessionCloner.Clone(original.Plan) : SessionCloner.Clone(plan);
        original.AppearanceByCharacterId = CloneMap(appearances ?? original.AppearanceByCharacterId);
        original.PrivateIntentByCharacterId = CloneMap(privateIntents ?? original.PrivateIntentByCharacterId);
        TranscriptProjector.Apply(Document, now);
        await SaveTranscriptAsync();
    });

    public async Task RecastTurnAsync(string turnId, RpCharacter? author) => await RunExclusiveAsync("Changing author...", async () =>
    {
        if (Document is null)
            return;

        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var now = DateTime.UtcNow;
        var authorName = author?.Name ?? "Narrator";
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = original.ParentTurnId,
            TurnNumber = NextTurnNumber(original.ParentTurnId),
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = "edited",
            AuthorCharacterId = author?.Id ?? "",
            AuthorName = authorName,
            ActorCharacterId = author?.Id ?? "",
            ActorName = authorName,
            Guidance = original.Guidance,
            Body = original.Body,
            Plan = SessionCloner.Clone(original.Plan),
            AppearanceByCharacterId = CloneMap(original.AppearanceByCharacterId),
            PrivateIntentByCharacterId = CloneMap(original.PrivateIntentByCharacterId),
            Scene = SessionCloner.Clone(original.Scene)
        };
        CommitTurn(turn, now);
        await SaveTranscriptAsync();
    });

    public async Task SavePlanAsync(
        string turnId,
        RpTurnPlan plan,
        IReadOnlyDictionary<string, string> appearances,
        IReadOnlyDictionary<string, string> privateIntents) => await RunExclusiveAsync("Saving plan...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var turn = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (turn is null)
            return;

        turn.Plan = SessionCloner.Clone(plan);
        turn.AppearanceByCharacterId = CloneMap(appearances);
        turn.PrivateIntentByCharacterId = CloneMap(privateIntents);
        turn.UpdatedUtc = DateTime.UtcNow;
        await SaveTranscriptAsync();
    });

    public async Task SelectSiblingAsync(string turnId) => await RunExclusiveAsync("Switching branch...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        if (TranscriptGraph.FindTurn(Document.Transcript, turnId) is null)
            return;

        TranscriptGraph.ClearWorkingScene(Document.Transcript);
        TranscriptGraph.SelectLeaf(Document.Transcript, ResolveLeafFrom(turnId));
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    });

    public async Task DeleteTurnAsync(string id) => await RunExclusiveAsync("Deleting...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var turn = TranscriptGraph.FindTurn(Document.Transcript, id);
        if (turn is null)
            return;

        var children = TranscriptGraph.GetChildren(Document.Transcript, turn.Id);
        foreach (var child in children)
            child.ParentTurnId = turn.ParentTurnId;

        await RemoveSnapshotsForTurnsAsync([id]);
        Document.Transcript.Turns.RemoveAll(existing => existing.Id == id);
        Document.Transcript.DeletedTurnIds.Add(id);
        if (Document.Transcript.ActiveLeafTurnId == id)
            Document.Transcript.ActiveLeafTurnId = children.LastOrDefault()?.Id ?? turn.ParentTurnId;

        TranscriptGraph.ClearWorkingScene(Document.Transcript);
        TranscriptGraph.RepairSelections(Document.Transcript);
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    });

    public async Task DeleteBranchAsync(string id) => await RunExclusiveAsync("Deleting branch...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var toDelete = CollectSubtreeIds(id);
        if (toDelete.Count == 0)
            return;

        var parentId = TranscriptGraph.FindTurn(Document.Transcript, id)?.ParentTurnId ?? "";
        await RemoveSnapshotsForTurnsAsync(toDelete);
        Document.Transcript.Turns.RemoveAll(turn => toDelete.Contains(turn.Id));
        foreach (var turnId in toDelete)
            Document.Transcript.DeletedTurnIds.Add(turnId);
        if (toDelete.Contains(Document.Transcript.ActiveLeafTurnId))
            Document.Transcript.ActiveLeafTurnId = TranscriptGraph.GetChildren(Document.Transcript, parentId).LastOrDefault()?.Id ?? parentId;

        TranscriptGraph.ClearWorkingScene(Document.Transcript);
        TranscriptGraph.RepairSelections(Document.Transcript);
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    });

    public async Task ApplySceneStateAsync(RpSceneFrame scene) => await RunExclusiveAsync("Updating scene...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var target = TranscriptGraph.GetEditableWorkingScene(Document.Transcript);
        target.LocationId = scene.LocationId;
        target.LocationName = scene.LocationName;
        target.InSceneCharacterIds = [.. scene.InSceneCharacterIds];
        target.InSceneItemIds = [.. scene.InSceneItemIds];
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    });

    async Task SetOptionAsync(Action<RpTranscriptOptionsState> update)
    {
        if (Document is null)
            return;

        update(Document.Transcript.Options);
        await SaveTranscriptAsync();
    }

    async Task RunExclusiveAsync(string busyMessage, Func<Task> action)
    {
        lock (_operationLock)
        {
            if (IsBusy)
                return;

            IsBusy = true;
            BusyMessage = busyMessage;
        }

        await NotifyChangedAsync();
        try
        {
            await action();
        }
        finally
        {
            lock (_operationLock)
            {
                IsBusy = false;
                BusyMessage = "";
            }

            await NotifyChangedAsync();
        }
    }

    async Task<RpTranscriptTurn?> GenerateTurnCoreAsync(string parentTurnId, string guidance, RpCharacter? requestedActor, bool requestedNarrator, string turnShape, string mode, RpSceneFrame? sceneOverride = null)
    {
        if (Document is null)
            return null;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateTurnAsync(
                Document,
                providers.Items.ToList(),
                modelSelection.State,
                new(
                    parentTurnId,
                    mode,
                    guidance,
                    turnShape,
                    requestedActor?.Id ?? "",
                    requestedActor?.Name ?? "",
                    requestedNarrator,
                    sceneOverride),
                new(UpdateActiveTraceAsync, UpdateActiveDraftAsync));
            return await CommitGeneratedTurnAsync(parentTurnId, guidance, mode, result);
        }
        catch (TranscriptGenerationException exception)
        {
            ClearActiveDraft();
            PersistFailedTurn(parentTurnId, guidance, requestedActor, requestedNarrator, mode, turnShape, exception.Trace, scene: sceneOverride);
            CaptureBackgroundError(exception, logger, "Generating transcript turn failed with a model trace.");
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
        catch (Exception exception)
        {
            ClearActiveDraft();
            CaptureBackgroundError(exception, logger, "Generating transcript turn failed unexpectedly.");
            if (mode is "regenerated" or "replanned")
            {
                PersistFailedTurn(parentTurnId, guidance, requestedActor, requestedNarrator, mode, turnShape, BuildUnhandledFailureTrace(exception), scene: sceneOverride);
                await SaveTranscriptAsync();
            }
            else
                await NotifyChangedAsync();

            await ClearActiveTraceAsync();
            return null;
        }
    }

    async Task<RpTranscriptTurn?> GenerateProseFromPlanCoreAsync(GenerateProseFromPlanRequest request)
    {
        if (Document is null)
            return null;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateProseFromPlanAsync(
                Document,
                providers.Items.ToList(),
                modelSelection.State,
                request,
                new(UpdateActiveTraceAsync, UpdateActiveDraftAsync));
            return await CommitGeneratedTurnAsync(request.ParentTurnId, request.Guidance, request.Mode, result);
        }
        catch (TranscriptGenerationException exception)
        {
            ClearActiveDraft();
            PersistFailedTurn(
                request.ParentTurnId,
                request.Guidance,
                request.RequestedNarrator ? null : new() { Id = request.ActorCharacterId, Name = request.ActorName },
                request.RequestedNarrator,
                request.Mode,
                request.Plan.TurnShape,
                exception.Trace,
                request.Plan,
                request.AppearanceByCharacterId,
                request.PrivateIntentByCharacterId,
                request.Scene);
            CaptureBackgroundError(exception, logger, "Replanning transcript turn failed with a model trace.");
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
        catch (Exception exception)
        {
            ClearActiveDraft();
            CaptureBackgroundError(exception, logger, "Replanning transcript turn failed unexpectedly.");
            PersistFailedTurn(
                request.ParentTurnId,
                request.Guidance,
                request.RequestedNarrator ? null : new() { Id = request.ActorCharacterId, Name = request.ActorName },
                request.RequestedNarrator,
                request.Mode,
                request.Plan.TurnShape,
                BuildUnhandledFailureTrace(exception),
                request.Plan,
                request.AppearanceByCharacterId,
                request.PrivateIntentByCharacterId,
                request.Scene);
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
    }

    async Task<RpTranscriptTurn?> GeneratePlanAndProseCoreAsync(GeneratePlanAndProseRequest request)
    {
        if (Document is null)
            return null;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GeneratePlanAndProseAsync(
                Document,
                providers.Items.ToList(),
                modelSelection.State,
                request,
                new(UpdateActiveTraceAsync, UpdateActiveDraftAsync));
            return await CommitGeneratedTurnAsync(request.ParentTurnId, request.Guidance, request.Mode, result);
        }
        catch (TranscriptGenerationException exception)
        {
            ClearActiveDraft();
            PersistFailedTurn(
                request.ParentTurnId,
                request.Guidance,
                request.RequestedNarrator ? null : new() { Id = request.ActorCharacterId, Name = request.ActorName },
                request.RequestedNarrator,
                request.Mode,
                request.RequestedTurnShape,
                exception.Trace,
                appearances: request.AppearanceByCharacterId,
                scene: request.Scene);
            CaptureBackgroundError(exception, logger, "Regenerating transcript turn failed with a model trace.");
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
        catch (Exception exception)
        {
            ClearActiveDraft();
            CaptureBackgroundError(exception, logger, "Regenerating transcript turn failed unexpectedly.");
            PersistFailedTurn(
                request.ParentTurnId,
                request.Guidance,
                request.RequestedNarrator ? null : new() { Id = request.ActorCharacterId, Name = request.ActorName },
                request.RequestedNarrator,
                request.Mode,
                request.RequestedTurnShape,
                BuildUnhandledFailureTrace(exception),
                appearances: request.AppearanceByCharacterId,
                scene: request.Scene);
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
            return null;
        }
    }

    async Task<RpTranscriptTurn> CommitGeneratedTurnAsync(string parentTurnId, string guidance, string mode, GeneratedTurnResult result)
    {
        result.Trace.Data["actorName"] = result.ActorName;
        var now = DateTime.UtcNow;
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = parentTurnId,
            TurnNumber = NextTurnNumber(parentTurnId),
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = NormalizeMode(mode),
            AuthorCharacterId = result.ActorCharacterId,
            AuthorName = string.IsNullOrWhiteSpace(result.ActorName) ? "Narrator" : result.ActorName,
            ActorCharacterId = result.ActorCharacterId,
            ActorName = result.ActorName,
            Guidance = guidance.Trim(),
            Body = result.Body,
            Plan = SessionCloner.Clone(result.Plan),
            AppearanceByCharacterId = CloneMap(result.AppearanceByCharacterId),
            PrivateIntentByCharacterId = CloneMap(result.PrivateIntentByCharacterId),
            Scene = SessionCloner.Clone(result.Scene),
            Trace = SessionCloner.Clone(result.Trace)
        };
        ClearActiveDraft();
        CommitTurn(turn, now);
        await SaveTranscriptAsync();
        await ClearActiveTraceAsync();
        return turn;
    }

    async Task UpdateActiveTraceAsync(RpTurnTrace trace)
    {
        ActiveTrace = SessionCloner.Clone(trace);
        await NotifyChangedAsync();
    }

    async Task UpdateActiveDraftAsync(TranscriptProseUpdate update)
    {
        var now = DateTime.UtcNow;
        var existing = ActiveDraftTurn;
        ActiveDraftTurn = new()
        {
            Id = existing?.Id ?? "__draft-turn",
            ParentTurnId = update.ParentTurnId,
            TurnNumber = NextTurnNumber(update.ParentTurnId),
            CreatedUtc = existing?.CreatedUtc ?? now,
            UpdatedUtc = now,
            Mode = NormalizeMode(update.Mode),
            AuthorCharacterId = update.ActorCharacterId,
            AuthorName = string.IsNullOrWhiteSpace(update.ActorName) ? "Narrator" : update.ActorName,
            ActorCharacterId = update.ActorCharacterId,
            ActorName = update.ActorName,
            Guidance = update.Guidance.Trim(),
            Body = update.Body,
            Plan = SessionCloner.Clone(update.Plan),
            Scene = SessionCloner.Clone(update.Scene)
        };
        await NotifyChangedAsync();
    }

    void ClearActiveDraft()
    {
        ActiveDraftTurn = null;
    }

    async Task ClearActiveTraceAsync()
    {
        ActiveTrace = null;
        await NotifyChangedAsync();
    }

    RpTurnTrace BuildUnhandledFailureTrace(Exception exception)
    {
        var now = DateTime.UtcNow;
        var trace = ActiveTrace is null
            ? new RpTurnTrace { StartedUtc = now }
            : SessionCloner.Clone(ActiveTrace);
        if (trace.StartedUtc == default)
            trace.StartedUtc = now;

        trace.Status = "failed";
        trace.CompletedUtc = now;
        trace.DurationSeconds = (trace.CompletedUtc - trace.StartedUtc).TotalSeconds;
        trace.Data["error"] = exception.Message;
        var step = trace.Steps.FirstOrDefault(step => step.Status is "running" or "pending");
        if (step is null)
        {
            step = new() { Id = "generation", Label = "Generation", StartedUtc = trace.StartedUtc };
            trace.Steps.Add(step);
        }

        step.Status = "failed";
        step.CompletedUtc = now;
        step.DurationSeconds = (step.CompletedUtc - step.StartedUtc).TotalSeconds;
        step.Error = exception.Message;
        trace.Summary = $"Failed · {string.Join(" -> ", trace.Steps.Select(step => step.Label))}";
        return trace;
    }

    void PersistFailedTurn(
        string parentTurnId,
        string guidance,
        RpCharacter? requestedActor,
        bool requestedNarrator,
        string mode,
        string turnShape,
        RpTurnTrace trace,
        RpTurnPlan? plan = null,
        IReadOnlyDictionary<string, string>? appearances = null,
        IReadOnlyDictionary<string, string>? privateIntents = null,
        RpSceneFrame? scene = null)
    {
        if (Document is null)
            return;

        var actorName = requestedNarrator ? "Narrator" : requestedActor?.Name ?? "Narrator";
        var actorId = requestedNarrator ? "" : requestedActor?.Id ?? "";
        trace.Data["actorName"] = actorName;
        var now = DateTime.UtcNow;
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = parentTurnId,
            TurnNumber = NextTurnNumber(parentTurnId),
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = NormalizeMode(mode),
            AuthorCharacterId = actorId,
            AuthorName = actorName,
            ActorCharacterId = actorId,
            ActorName = actorName,
            Guidance = guidance.Trim(),
            Plan = plan is null ? new() { TurnShape = turnShape } : SessionCloner.Clone(plan),
            AppearanceByCharacterId = appearances is null ? [] : CloneMap(appearances),
            PrivateIntentByCharacterId = privateIntents is null ? [] : CloneMap(privateIntents),
            Scene = SessionCloner.Clone(scene ?? TranscriptGraph.GetSceneForNextTurn(Document.Transcript, parentTurnId)),
            Trace = SessionCloner.Clone(trace)
        };
        CommitTurn(turn, now);
    }

    void SelectRegenerationParent(string parentTurnId)
    {
        if (Document is null)
            return;

        if (string.IsNullOrWhiteSpace(parentTurnId))
        {
            Document.Transcript.ActiveLeafTurnId = "";
            Document.Transcript.BranchSelections.Remove(TranscriptGraph.RootBranchKey);
            TranscriptGraph.ClearWorkingScene(Document.Transcript);
            TranscriptProjector.Apply(Document);
            return;
        }

        TranscriptGraph.SelectLeaf(Document.Transcript, parentTurnId);
        TranscriptGraph.ClearWorkingScene(Document.Transcript);
        TranscriptProjector.Apply(Document);
    }

    void CommitTurn(RpTranscriptTurn turn, DateTime now)
    {
        if (Document is null)
            return;

        if (turn.TurnNumber <= 0)
            turn.TurnNumber = NextTurnNumber(turn.ParentTurnId);
        Document.Transcript.Turns.Add(turn);
        TranscriptGraph.ClearWorkingSceneForParent(Document.Transcript, turn.ParentTurnId);
        TranscriptGraph.SelectLeaf(Document.Transcript, turn.Id);
        TranscriptProjector.Apply(Document, now);
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        TranscriptProjector.Apply(Document);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
    }

    async Task SaveTranscriptAndTimelineAsync()
    {
        if (Document is null)
            return;

        TranscriptProjector.Apply(Document);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Timeline);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Transcript);
        await NotifyActiveDocumentChangedAsync(RoleplayStoreArea.Timeline);
    }

    async Task DiscardSpeechAsync(RpTranscriptTurn turn)
    {
        if (messageSpeechService is not null)
        {
            await messageSpeechService.DiscardTurnSpeechAsync(turn);
            return;
        }

        turn.Speech = new();
    }

    HashSet<string> CollectSubtreeIds(string rootId)
    {
        if (Document is null)
            return [];

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(rootId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!ids.Add(current))
                continue;

            foreach (var child in TranscriptGraph.GetChildren(Document.Transcript, current))
                stack.Push(child.Id);
        }

        return ids;
    }

    string ResolveLeafFrom(string turnId)
    {
        if (Document is null)
            return turnId;

        var currentId = turnId;
        while (true)
        {
            var children = TranscriptGraph.GetChildren(Document.Transcript, currentId);
            if (children.Count == 0)
                return currentId;

            var selectionKey = TranscriptGraph.BranchKey(currentId);
            var selectedChild = Document.Transcript.BranchSelections.TryGetValue(selectionKey, out var selectedId)
                ? children.FirstOrDefault(child => child.Id == selectedId)
                : null;
            currentId = (selectedChild ?? children.Last()).Id;
        }
    }

    static Dictionary<string, string> CloneMap(IReadOnlyDictionary<string, string> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    static string NormalizeMode(string mode) => mode switch
    {
        "guided" => "guided",
        "automatic" => "automatic",
        "regenerated" => "regenerated",
        "replanned" => "replanned",
        "edited" => "edited",
        _ => "manual"
    };

    int NextTurnNumber(string parentTurnId) =>
        Document is null ? 1 : TranscriptTurnNumbering.NextTurnNumber(Document.Transcript, parentTurnId);

    static string NextTurnId() => $"turn-{Guid.NewGuid():N}";
    static string NextSnapshotId() => $"snap-{Guid.NewGuid():N}";
    static string NextTimelineId() => $"t-{Guid.NewGuid():N}";
}
