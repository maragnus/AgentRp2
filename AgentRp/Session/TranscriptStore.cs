using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public sealed class TranscriptStore(
    ActiveChatContext activeChat,
    ChatRegistry registry,
    ProviderStore providers,
    ITextGenerationService textGenerationService) : ActiveChatStoreBase(activeChat, registry)
{
    protected override RoleplayStoreArea Area => RoleplayStoreArea.Transcript;

    public RpTranscriptState State => Document?.Transcript ?? new();
    public RpTranscriptOptionsState Options => State.Options;
    public List<RpTranscriptTurn> Items => Document is null ? [] : TranscriptGraph.GetActivePath(Document.Transcript);
    public RpTranscriptTurn? ActiveLeaf => Document is null ? null : TranscriptGraph.FindTurn(Document.Transcript, Document.Transcript.ActiveLeafTurnId);
    public bool IsBusy { get; private set; }
    public string BusyMessage { get; private set; } = "";
    public RpTurnTrace? ActiveTrace { get; private set; }

    readonly object _operationLock = new();

    public RpTranscriptSnapshot? SnapshotFor(string turnId) =>
        Document is null ? null : TranscriptGraph.FindSnapshotByTurn(Document.Transcript, turnId);

    public IReadOnlyList<RpTranscriptTurn> SiblingsFor(string turnId) =>
        Document is null ? [] : TranscriptGraph.GetSiblings(Document.Transcript, turnId);

    public async Task SetInjectAudioTagsAsync(bool value) => await SetOptionAsync(options => options.InjectAudioTags = value);

    public async Task SetHideAudioTagsAsync(bool value) => await SetOptionAsync(options => options.HideAudioTags = value);

    public async Task SetShowAppearanceBlocksAsync(bool value) => await SetOptionAsync(options => options.ShowAppearanceBlocks = value);

    public async Task SetShowProcessTracesAsync(bool value) => await SetOptionAsync(options => options.ShowProcessTraces = value);

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
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = "manual",
            AuthorCharacterId = speaker?.Id ?? "",
            AuthorName = authorName,
            ActorCharacterId = speaker?.Id ?? "",
            ActorName = authorName,
            Body = text.Trim(),
            Scene = SessionCloner.Clone(TranscriptGraph.GetActiveScene(Document.Transcript))
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

    public async Task RegenerateAsync(string turnId, string guidance, RpCharacter? requestedActor, string turnShape) => await RunExclusiveAsync("Regenerating...", async () =>
    {
        if (Document is null)
            return;

        var original = TranscriptGraph.FindTurn(Document.Transcript, turnId);
        if (original is null)
            return;

        var requestedNarrator = requestedActor is null && string.IsNullOrWhiteSpace(original.ActorCharacterId);
        var actor = requestedNarrator
            ? null
            : requestedActor ?? Document.Characters.FirstOrDefault(character => character.Id == original.ActorCharacterId);
        await GenerateTurnCoreAsync(
            parentTurnId: original.ParentTurnId,
            guidance: string.IsNullOrWhiteSpace(guidance) ? original.Guidance : guidance,
            requestedActor: actor,
            requestedNarrator: requestedNarrator,
            turnShape: string.IsNullOrWhiteSpace(turnShape) ? original.Plan.TurnShape : turnShape,
            mode: "regenerated");
    });

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

        var now = DateTime.UtcNow;
        var turn = new RpTranscriptTurn
        {
            Id = NextTurnId(),
            ParentTurnId = original.ParentTurnId,
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = "edited",
            AuthorCharacterId = original.AuthorCharacterId,
            AuthorName = original.AuthorName,
            ActorCharacterId = original.ActorCharacterId,
            ActorName = original.ActorName,
            Guidance = original.Guidance,
            Body = body.Trim(),
            Plan = plan is null ? SessionCloner.Clone(original.Plan) : SessionCloner.Clone(plan),
            AppearanceByCharacterId = CloneMap(appearances ?? original.AppearanceByCharacterId),
            PrivateIntentByCharacterId = CloneMap(privateIntents ?? original.PrivateIntentByCharacterId),
            Scene = SessionCloner.Clone(original.Scene)
        };
        CommitTurn(turn, now);
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

    public async Task CreateSnapshotAsync(string turnId) => await RunExclusiveAsync("Creating snapshot...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateSnapshotAsync(
                Document,
                providers.Items.ToList(),
                new(turnId));
            var snapshot = TranscriptGraph.FindSnapshotByTurn(Document.Transcript, turnId) ?? new RpTranscriptSnapshot { Id = NextSnapshotId(), TurnId = turnId };
            RemoveSnapshotTimelineEntries(snapshot);
            snapshot.CreatedUtc = DateTime.UtcNow;
            snapshot.Summary = result.Summary;
            snapshot.EarlierPrivateIntentContinuity = result.EarlierPrivateIntentContinuity;
            snapshot.Facts = result.Facts.Select(CloneSnapshotFact).ToList();
            snapshot.TimelineEntries = result.TimelineEntries.Select(CloneSnapshotTimelineEntry).ToList();
            AddSnapshotTimelineEntries(snapshot);
            snapshot.CharacterAppearances = CloneMap(result.CharacterAppearances);
            snapshot.Scene = SessionCloner.Clone(result.Scene);
            snapshot.Trace = SessionCloner.Clone(result.Trace);
            if (Document.Transcript.Snapshots.All(existing => existing.Id != snapshot.Id))
                Document.Transcript.Snapshots.Add(snapshot);

            var turn = TranscriptGraph.FindTurn(Document.Transcript, turnId);
            if (turn is not null)
            {
                turn.SnapshotId = snapshot.Id;
                turn.UpdatedUtc = DateTime.UtcNow;
            }

            TranscriptProjector.Apply(Document);
            await SaveTranscriptAndTimelineAsync();
        }
        catch (TranscriptGenerationException exception)
        {
            CaptureBackgroundError(exception);
            await NotifyChangedAsync();
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
            await NotifyChangedAsync();
        }
    });

    public async Task SelectSiblingAsync(string turnId) => await RunExclusiveAsync("Switching branch...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        if (TranscriptGraph.FindTurn(Document.Transcript, turnId) is null)
            return;

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

        Document.Transcript.Snapshots.RemoveAll(snapshot => snapshot.TurnId == id);
        Document.Transcript.Turns.RemoveAll(existing => existing.Id == id);
        if (Document.Transcript.ActiveLeafTurnId == id)
            Document.Transcript.ActiveLeafTurnId = children.LastOrDefault()?.Id ?? turn.ParentTurnId;

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
        Document.Transcript.Snapshots.RemoveAll(snapshot => toDelete.Contains(snapshot.TurnId));
        Document.Transcript.Turns.RemoveAll(turn => toDelete.Contains(turn.Id));
        if (toDelete.Contains(Document.Transcript.ActiveLeafTurnId))
            Document.Transcript.ActiveLeafTurnId = TranscriptGraph.GetChildren(Document.Transcript, parentId).LastOrDefault()?.Id ?? parentId;

        TranscriptGraph.RepairSelections(Document.Transcript);
        TranscriptProjector.Apply(Document);
        await SaveTranscriptAsync();
    });

    public async Task ApplySceneStateAsync(RpSceneFrame scene) => await RunExclusiveAsync("Updating scene...", async () =>
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        var target = TranscriptGraph.GetEditableActiveScene(Document.Transcript);
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

    async Task GenerateTurnCoreAsync(string parentTurnId, string guidance, RpCharacter? requestedActor, bool requestedNarrator, string turnShape, string mode)
    {
        if (Document is null)
            return;

        ClearBackgroundError();
        try
        {
            var result = await textGenerationService.GenerateTurnAsync(
                Document,
                providers.Items.ToList(),
                new(
                    parentTurnId,
                    mode,
                    guidance,
                    turnShape,
                    requestedActor?.Id ?? "",
                    requestedActor?.Name ?? "",
                    requestedNarrator),
                new(UpdateActiveTraceAsync));
            result.Trace.Data["actorName"] = result.ActorName;
            var now = DateTime.UtcNow;
            var turn = new RpTranscriptTurn
            {
                Id = NextTurnId(),
                ParentTurnId = parentTurnId,
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
            CommitTurn(turn, now);
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
        }
        catch (TranscriptGenerationException exception)
        {
            PersistFailedTurn(parentTurnId, guidance, requestedActor, requestedNarrator, mode, exception.Trace);
            CaptureBackgroundError(exception);
            await SaveTranscriptAsync();
            await ClearActiveTraceAsync();
        }
        catch (Exception exception)
        {
            CaptureBackgroundError(exception);
            await NotifyChangedAsync();
            await ClearActiveTraceAsync();
        }
    }

    async Task UpdateActiveTraceAsync(RpTurnTrace trace)
    {
        ActiveTrace = SessionCloner.Clone(trace);
        await NotifyChangedAsync();
    }

    async Task ClearActiveTraceAsync()
    {
        ActiveTrace = null;
        await NotifyChangedAsync();
    }

    void PersistFailedTurn(string parentTurnId, string guidance, RpCharacter? requestedActor, bool requestedNarrator, string mode, RpTurnTrace trace)
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
            CreatedUtc = now,
            UpdatedUtc = now,
            Mode = NormalizeMode(mode),
            AuthorCharacterId = actorId,
            AuthorName = actorName,
            ActorCharacterId = actorId,
            ActorName = actorName,
            Guidance = guidance.Trim(),
            Scene = SessionCloner.Clone(TranscriptGraph.GetActiveScene(Document.Transcript)),
            Trace = SessionCloner.Clone(trace)
        };
        CommitTurn(turn, now);
    }

    void CommitTurn(RpTranscriptTurn turn, DateTime now)
    {
        if (Document is null)
            return;

        Document.Transcript.Turns.Add(turn);
        TranscriptGraph.SelectLeaf(Document.Transcript, turn.Id);
        TranscriptProjector.Apply(Document, now);
    }

    async Task SaveTranscriptAsync()
    {
        if (Document is null)
            return;

        TranscriptProjector.Apply(Document);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await NotifyChangedAsync();
    }

    async Task SaveTranscriptAndTimelineAsync()
    {
        if (Document is null)
            return;

        TranscriptProjector.Apply(Document);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Transcript);
        await Registry.ReplaceAreaAsync(Document, RoleplayStoreArea.Timeline);
        await NotifyChangedAsync();
    }

    void RemoveSnapshotTimelineEntries(RpTranscriptSnapshot snapshot)
    {
        if (Document is null || snapshot.TimelineEntries.Count == 0)
            return;

        var ids = snapshot.TimelineEntries
            .Select(entry => entry.TimelineEntryId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        Document.Timeline.RemoveAll(entry => ids.Contains(entry.Id));
    }

    void AddSnapshotTimelineEntries(RpTranscriptSnapshot snapshot)
    {
        if (Document is null)
            return;

        foreach (var entry in snapshot.TimelineEntries)
        {
            var timelineEntry = new RpTimelineEntry
            {
                Id = NextTimelineId(),
                Title = entry.Title,
                Date = string.IsNullOrWhiteSpace(entry.WhenText) ? "Snapshot" : entry.WhenText,
                Description = BuildSnapshotTimelineDescription(entry),
                Characters = [.. entry.CharacterNames],
                Significance = "Generated from snapshot."
            };
            entry.TimelineEntryId = timelineEntry.Id;
            Document.Timeline.Add(timelineEntry);
        }
    }

    static RpTranscriptSnapshotFact CloneSnapshotFact(RpTranscriptSnapshotFact value) => new()
    {
        Title = value.Title,
        Summary = value.Summary,
        Details = value.Details,
        CharacterNames = [.. value.CharacterNames],
        LocationNames = [.. value.LocationNames],
        ItemNames = [.. value.ItemNames]
    };

    static RpTranscriptSnapshotTimelineEntry CloneSnapshotTimelineEntry(RpTranscriptSnapshotTimelineEntry value) => new()
    {
        TimelineEntryId = value.TimelineEntryId,
        WhenText = value.WhenText,
        Title = value.Title,
        Summary = value.Summary,
        Details = value.Details,
        CharacterNames = [.. value.CharacterNames],
        LocationNames = [.. value.LocationNames],
        ItemNames = [.. value.ItemNames]
    };

    static string BuildSnapshotTimelineDescription(RpTranscriptSnapshotTimelineEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Details))
            return entry.Summary;

        if (string.IsNullOrWhiteSpace(entry.Summary))
            return entry.Details;

        return $"{entry.Summary}\n\n{entry.Details}";
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
        "edited" => "edited",
        _ => "manual"
    };

    static string NextTurnId() => $"turn-{Guid.NewGuid():N}";
    static string NextSnapshotId() => $"snap-{Guid.NewGuid():N}";
    static string NextTimelineId() => $"t-{Guid.NewGuid():N}";
}
