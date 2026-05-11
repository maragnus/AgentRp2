using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

public enum SnapshotDeleteMethod
{
    Unwrap,
    DeleteCoveredMessages
}

public sealed record SnapshotDeleteImpact(
    string SnapshotId,
    int CoveredTurnCount,
    int LinkedTimelineEntryCount,
    bool IsOnActivePath,
    bool IsLatestOnActivePath,
    int LaterSnapshotCount,
    int CoveredBranchCount)
{
    public bool CanUnwrap => IsOnActivePath && IsLatestOnActivePath;
    public bool CanDeleteCoveredMessages => CoveredTurnCount > 0;
    public string UnwrapDisabledReason => IsOnActivePath
        ? "Only the latest snapshot can be unwrapped."
        : "Only snapshots on the active branch can be unwrapped.";
    public string DeleteCoveredMessagesDisabledReason => "This snapshot has no linked messages.";
}

public sealed record SnapshotDraftTarget(
    string RequestedTurnId,
    string TargetTurnId,
    int CoveredTurnCount,
    int UnsnapshottedTurnCount,
    string DisabledReason)
{
    public bool CanCreate => string.IsNullOrWhiteSpace(DisabledReason);
}

public sealed partial class TranscriptStore
{
    public SnapshotDraftTarget? GetSnapshotDraftTarget(string turnId) =>
        Document is null ? null : SnapshotPath.ResolveDraftTarget(Document, turnId);

    public bool CanCreateSnapshotAt(string turnId) => GetSnapshotDraftTarget(turnId)?.CanCreate == true;

    public SnapshotDraftTarget? GetSnapshotSuggestionTarget(int retainedTurnCount, int suggestionThreshold)
    {
        if (Document is null)
            return null;

        var activePath = TranscriptGraph.GetActivePath(Document.Transcript);
        if (activePath.Count == 0)
            return null;

        var snapshots = SnapshotPath.GetSnapshotsOnPath(Document.Transcript, activePath);
        var latestSnapshot = snapshots.LastOrDefault();
        var latestSnapshotIndex = latestSnapshot is null
            ? -1
            : activePath.ToList().FindIndex(turn => string.Equals(turn.Id, latestSnapshot.TurnId, StringComparison.Ordinal));
        var unsnapshottedTurnCount = activePath.Count - latestSnapshotIndex - 1;
        if (unsnapshottedTurnCount < Math.Max(suggestionThreshold, SnapshotPath.MinimumEligibleCompletedTurns))
            return null;

        var targetIndex = activePath.Count - Math.Max(1, retainedTurnCount) - 1;
        if (targetIndex < 0 || targetIndex >= activePath.Count)
            return null;

        var target = SnapshotPath.ResolveDraftTarget(Document, activePath[targetIndex].Id);
        return target?.CanCreate == true ? target : null;
    }

    public IReadOnlyList<RpTranscriptSnapshot> SnapshotsForActivePath()
    {
        if (Document is null)
            return [];

        var path = TranscriptGraph.GetActivePath(Document.Transcript);
        return SnapshotPath.GetSnapshotsOnPath(Document.Transcript, path);
    }

    public int SnapshotTurnCount(string snapshotId) =>
        Document is null
            ? 0
            : Document.Transcript.Turns.Count(turn => string.Equals(turn.SnapshotId, snapshotId, StringComparison.Ordinal));

    public SnapshotDeleteImpact? GetSnapshotDeleteImpact(string snapshotId)
    {
        if (Document is null)
            return null;

        var snapshot = TranscriptGraph.FindSnapshot(Document.Transcript, snapshotId);
        if (snapshot is null)
            return null;

        return BuildSnapshotDeleteImpact(snapshot);
    }

    public RpTranscriptSnapshotDraftPreview? PreviewSnapshotDraft(string turnId)
    {
        if (Document is null)
            return null;

        var target = GetSnapshotDraftTarget(turnId);
        if (target is null)
            return null;

        if (!target.CanCreate)
            return new() { TurnId = target.TargetTurnId, CoveredTurnCount = target.CoveredTurnCount };

        var context = SnapshotPath.Build(Document, target.TargetTurnId);
        if (context.CoveredTurns.Count == 0)
            return new() { TurnId = context.TargetTurn.Id };

        var firstTurn = context.CoveredTurns.First();
        var lastTurn = context.CoveredTurns.Last();
        var firstDraftTurn = ToDraftTurn(firstTurn);
        var lastDraftTurn = ToDraftTurn(lastTurn);
        return new()
        {
            TurnId = turnId,
            CoveredTurnCount = context.CoveredTurns.Count,
            FirstSpeakerName = firstDraftTurn.SpeakerName,
            FirstTurnNumber = firstTurn.TurnNumber,
            LastSpeakerName = lastDraftTurn.SpeakerName,
            LastTurnNumber = lastTurn.TurnNumber,
            LatestSnapshotUtc = context.LatestSnapshot?.CreatedUtc
        };
    }

    public async Task<MessageSpeechPlayback?> GetOrGenerateSnapshotSpeechAsync(string snapshotId, bool regenerate, CancellationToken cancellationToken = default)
    {
        MessageSpeechPlayback? playback = null;
        await RunExclusiveAsync(regenerate ? "Regenerating snapshot speech..." : "Generating snapshot speech...", async () =>
        {
            if (Document is null || messageSpeechService is null)
                return;

            ClearBackgroundError();
            var snapshot = TranscriptGraph.FindSnapshot(Document.Transcript, snapshotId);
            if (snapshot is null)
                return;

            playback = await messageSpeechService.GetOrGenerateSnapshotAsync(
                Document,
                providers.Items.ToList(),
                modelSelection.State,
                snapshot,
                regenerate,
                cancellationToken);
            await SaveTranscriptAsync();
        });

        return playback;
    }

    public async Task<RpTranscriptSnapshotDraft?> CreateSnapshotDraftAsync(string turnId, CancellationToken cancellationToken = default)
    {
        RpTranscriptSnapshotDraft? draft = null;
        await RunExclusiveAsync("Creating snapshot draft...", async () =>
        {
            if (Document is null)
                return;

            ClearBackgroundError();
            var context = SnapshotPath.Build(Document, turnId);
            if (context.CoveredTurns.Count == 0)
                throw new InvalidOperationException("Creating a snapshot draft failed because the selected turn is already covered by a snapshot.");

            var result = await textGenerationService.GenerateSnapshotAsync(
                Document,
                providers.Items.ToList(),
                modelSelection.State,
                new(context.TargetTurn.Id),
                cancellationToken);
            draft = new()
            {
                TurnId = context.TargetTurn.Id,
                CreatedUtc = DateTime.UtcNow,
                Summary = result.Summary,
                CoveredTurnIds = context.CoveredTurns.Select(turn => turn.Id).ToList(),
                IncludedTurns = context.CoveredTurns.Select(ToDraftTurn).ToList(),
                PrivateIntentByCharacterId = BuildSnapshotPrivateIntents(context.LatestSnapshot, context.CoveredTurns),
                CharacterAppearances = result.CharacterSceneStates ?? BuildSnapshotAppearances(context.LatestSnapshot, context.CoveredTurns),
                TimelineEntries = result.TimelineEntries.Select(CloneSnapshotTimelineEntry).ToList(),
                RelationshipUpdates = result.RelationshipUpdates?.Select(CloneSnapshotRelationshipUpdate).ToList() ?? [],
                Scene = SessionCloner.Clone(result.Scene ?? context.TargetTurn.Scene),
                Trace = SessionCloner.Clone(result.Trace)
            };
        });

        return draft;
    }

    public async Task CommitSnapshotDraftAsync(RpTranscriptSnapshotDraft draft, CancellationToken cancellationToken = default) =>
        await RunExclusiveAsync("Saving snapshot...", async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Document is null)
                return;

            ClearBackgroundError();
            if (string.IsNullOrWhiteSpace(draft.Summary))
                throw new InvalidOperationException("Saving the snapshot failed because the summary is empty.");
            if (draft.CoveredTurnIds.Count == 0)
                throw new InvalidOperationException("Saving the snapshot failed because no transcript turns were included.");

            var context = SnapshotPath.Build(Document, draft.TurnId, "Saving the snapshot failed");
            var includedIds = draft.CoveredTurnIds.ToHashSet(StringComparer.Ordinal);
            if (!includedIds.SetEquals(context.CoveredTurns.Select(turn => turn.Id)))
                throw new InvalidOperationException("Saving the snapshot failed because the transcript branch changed while the draft was open.");

            var existing = TranscriptGraph.FindSnapshotByTurn(Document.Transcript, draft.TurnId);
            if (existing is not null)
            {
                await DiscardSnapshotSpeechAsync(existing, cancellationToken);
                RemoveSnapshotTimelineEntries(existing);
                ClearSnapshotTurnLinks(existing);
                Document.Transcript.Snapshots.Remove(existing);
                Document.Transcript.DeletedSnapshotIds.Add(existing.Id);
            }

            var appliedRelationshipUpdates = draft.RelationshipUpdates.Where(update => update.ApplyChange).Select(CloneSnapshotRelationshipUpdate).ToList();
            var snapshot = new RpTranscriptSnapshot
            {
                Id = NextSnapshotId(),
                TurnId = draft.TurnId,
                StartTurnId = context.CoveredTurns.First().Id,
                EndTurnId = context.CoveredTurns.Last().Id,
                ParentBeforeStartTurnId = context.CoveredTurns.First().ParentTurnId,
                TurnNumberStart = context.CoveredTurns.First().TurnNumber,
                TurnNumberEnd = context.CoveredTurns.Last().TurnNumber,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                Summary = draft.Summary.Trim(),
                PrivateIntentByCharacterId = CloneMap(draft.PrivateIntentByCharacterId),
                CharacterAppearances = CloneMap(draft.CharacterAppearances),
                RelationshipUpdates = appliedRelationshipUpdates.Select(CloneSnapshotRelationshipUpdate).ToList(),
                Scene = SessionCloner.Clone(draft.Scene),
                Trace = draft.Trace is null ? null : SessionCloner.Clone(draft.Trace),
                IsActive = true
            };
            Document.Transcript.Snapshots.Add(snapshot);
            LinkSnapshotTurns(snapshot, context.CoveredTurns);
            AddSnapshotTimelineEntries(snapshot, draft.TimelineEntries);
            ApplySnapshotRelationshipUpdates(appliedRelationshipUpdates);

            await SaveSnapshotCommitAsync(appliedRelationshipUpdates.Count > 0);
        });

    public async Task DeleteSnapshotAsync(
        string snapshotId,
        SnapshotDeleteMethod method,
        bool removeTimelineEntries,
        CancellationToken cancellationToken = default) =>
        await RunExclusiveAsync("Deleting snapshot...", async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Document is null)
                return;

            ClearBackgroundError();
            var snapshot = TranscriptGraph.FindSnapshot(Document.Transcript, snapshotId);
            if (snapshot is null)
                return;

            var impact = BuildSnapshotDeleteImpact(snapshot);
            if (method == SnapshotDeleteMethod.Unwrap && !impact.CanUnwrap)
                throw new InvalidOperationException("Unwrapping the snapshot failed because only the latest snapshot on the active branch can be unwrapped.");
            if (method == SnapshotDeleteMethod.DeleteCoveredMessages && !impact.CanDeleteCoveredMessages)
                throw new InvalidOperationException("Deleting the snapshot messages failed because this snapshot has no linked messages.");

            await DiscardSnapshotSpeechAsync(snapshot, cancellationToken);
            if (removeTimelineEntries)
                RemoveSnapshotTimelineEntries(snapshot);
            else
                ClearSnapshotTimelineLinks(snapshot);

            if (method == SnapshotDeleteMethod.Unwrap)
                ClearSnapshotTurnLinks(snapshot);
            else
            {
                RemoveSnapshotTurns(snapshot);
                InvalidateCyoaDecision(CyoaDecisionInvalidationReason.TurnDeleted);
            }

            Document.Transcript.Snapshots.Remove(snapshot);
            Document.Transcript.DeletedSnapshotIds.Add(snapshot.Id);

            await SaveTranscriptAndTimelineAsync();
        });

    async Task RemoveSnapshotsForTurnsAsync(HashSet<string> turnIds)
    {
        if (Document is null)
            return;

        var affectedSnapshotIds = Document.Transcript.Turns
            .Where(turn => turnIds.Contains(turn.Id) && !string.IsNullOrWhiteSpace(turn.SnapshotId))
            .Select(turn => turn.SnapshotId)
            .ToHashSet(StringComparer.Ordinal);
        var snapshots = Document.Transcript.Snapshots
            .Where(snapshot => turnIds.Contains(snapshot.TurnId) || affectedSnapshotIds.Contains(snapshot.Id))
            .ToList();
        foreach (var snapshot in snapshots)
        {
            await DiscardSnapshotSpeechAsync(snapshot);
            ClearSnapshotTimelineLinks(snapshot);
            ClearSnapshotTurnLinks(snapshot);
        }

        var snapshotIds = snapshots.Select(snapshot => snapshot.Id).ToHashSet(StringComparer.Ordinal);
        Document.Transcript.Snapshots.RemoveAll(snapshot => snapshotIds.Contains(snapshot.Id));
        foreach (var snapshotId in snapshotIds)
            Document.Transcript.DeletedSnapshotIds.Add(snapshotId);
    }

    async Task DiscardSnapshotSpeechAsync(RpTranscriptSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (messageSpeechService is not null)
        {
            await messageSpeechService.DiscardSnapshotSpeechAsync(snapshot, cancellationToken);
            return;
        }

        snapshot.Speech = new();
    }

    void RemoveSnapshotTimelineEntries(RpTranscriptSnapshot snapshot)
    {
        if (Document is null)
            return;

        Document.Timeline.RemoveAll(entry => string.Equals(entry.SnapshotId, snapshot.Id, StringComparison.Ordinal));
    }

    void ClearSnapshotTimelineLinks(RpTranscriptSnapshot snapshot)
    {
        if (Document is null)
            return;

        foreach (var entry in Document.Timeline.Where(entry => string.Equals(entry.SnapshotId, snapshot.Id, StringComparison.Ordinal)))
            entry.SnapshotId = "";
    }

    void AddSnapshotTimelineEntries(RpTranscriptSnapshot snapshot, IEnumerable<RpTranscriptSnapshotTimelineEntry> entries)
    {
        if (Document is null)
            return;

        TranscriptTurnNumbering.EnsureTurnNumbers(Document.Transcript);
        var snapshotTurn = TranscriptGraph.FindTurn(Document.Transcript, snapshot.TurnId);
        var fallbackWhen = snapshotTurn is null ? "Snapshot" : TranscriptTurnNumbering.Format(snapshotTurn);
        foreach (var entry in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Title)))
        {
            var turnNumber = entry.TurnNumber > 0 ? entry.TurnNumber : snapshotTurn?.TurnNumber ?? 0;
            var timelineEntry = new RpTimelineEntry
            {
                Id = NextTimelineId(),
                SnapshotId = snapshot.Id,
                Title = entry.Title.Trim(),
                Date = turnNumber > 0 ? TranscriptTurnNumbering.Format(turnNumber) : fallbackWhen,
                Description = entry.Description.Trim(),
                CharacterIds = TimelineEntityLinkResolver.ResolveCharacterIds(Document.Characters, entry.CharacterIds),
                LocationIds = TimelineEntityLinkResolver.ResolveLocationIds(Document.Locations, entry.LocationIds),
                Significance = "Generated from snapshot."
            };
            Document.Timeline.Add(timelineEntry);
        }
    }

    void ApplySnapshotRelationshipUpdates(IEnumerable<RpTranscriptSnapshotRelationshipUpdate> updates)
    {
        if (Document is null)
            return;

        var now = DateTime.UtcNow;
        var characterNames = Document.Characters.ToDictionary(character => character.Id, character => character.Name, StringComparer.Ordinal);
        foreach (var update in updates.Where(update => update.ApplyChange))
        {
            var relationship = Document.CharacterRelationships.FirstOrDefault(relationship =>
                string.Equals(relationship.Id, update.RelationshipId, StringComparison.Ordinal)
                && string.Equals(relationship.CharacterAId, update.SourceCharacterId, StringComparison.Ordinal)
                && string.Equals(relationship.CharacterBId, update.TargetCharacterId, StringComparison.Ordinal));
            if (relationship is null)
                continue;

            var sourceName = characterNames.GetValueOrDefault(update.SourceCharacterId, "");
            var targetName = characterNames.GetValueOrDefault(update.TargetCharacterId, "");
            var view = CharacterRelationshipGraph.View(relationship, update.SourceCharacterId, sourceName, update.TargetCharacterId, targetName);
            view.RelationshipTypes = [.. update.RelationshipTypes];
            view.PrivateTensions = [.. update.PrivateTensions];
            view.HowSourceSeesTarget = update.HowSourceSeesTarget.Trim();
            view.HowTargetSeesSource = update.HowTargetSeesSource.Trim();
            view.PublicDynamic = update.PublicDynamic.Trim();
            StoryEntityTimestamps.Touch(relationship, now);
        }
    }

    void LinkSnapshotTurns(RpTranscriptSnapshot snapshot, IEnumerable<RpTranscriptTurn> turns)
    {
        var now = DateTime.UtcNow;
        var ordinal = 1;
        foreach (var turn in turns)
        {
            turn.SnapshotId = snapshot.Id;
            turn.ConsumedBySnapshotOrdinal = ordinal++;
            turn.UpdatedUtc = now;
        }
    }

    void ClearSnapshotTurnLinks(RpTranscriptSnapshot snapshot)
    {
        if (Document is null)
            return;

        var now = DateTime.UtcNow;
        foreach (var turn in Document.Transcript.Turns.Where(turn => string.Equals(turn.SnapshotId, snapshot.Id, StringComparison.Ordinal)))
        {
            turn.SnapshotId = "";
            turn.ConsumedBySnapshotOrdinal = null;
            turn.UpdatedUtc = now;
        }
    }

    void RemoveSnapshotTurns(RpTranscriptSnapshot snapshot)
    {
        if (Document is null)
            return;

        var turnIds = Document.Transcript.Turns
            .Where(turn => string.Equals(turn.SnapshotId, snapshot.Id, StringComparison.Ordinal))
            .Select(turn => turn.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (turnIds.Count == 0)
            return;

        ReparentChildrenOfRemovedTurns(turnIds);
        var activeLeafId = Document.Transcript.ActiveLeafTurnId;
        var activeLeafFallback = turnIds.Contains(activeLeafId)
            ? ResolveDeletedLeafFallback(activeLeafId, turnIds)
            : "";
        Document.Transcript.Turns.RemoveAll(turn => turnIds.Contains(turn.Id));
        foreach (var turnId in turnIds)
            Document.Transcript.DeletedTurnIds.Add(turnId);
        if (turnIds.Contains(activeLeafId))
            Document.Transcript.ActiveLeafTurnId = activeLeafFallback;

        TranscriptGraph.RepairSelections(Document.Transcript);
    }

    void ReparentChildrenOfRemovedTurns(HashSet<string> turnIds)
    {
        if (Document is null)
            return;

        foreach (var turn in Document.Transcript.Turns.Where(turn => !turnIds.Contains(turn.Id) && turnIds.Contains(turn.ParentTurnId)))
            turn.ParentTurnId = ResolveNearestSurvivingParent(turn.ParentTurnId, turnIds);
    }

    string ResolveDeletedLeafFallback(string deletedLeafId, HashSet<string> turnIds)
    {
        if (Document is null)
            return "";

        var fallback = ResolveNearestSurvivingParent(deletedLeafId, turnIds);
        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback;

        return Document.Transcript.Turns
            .Where(turn => !turnIds.Contains(turn.Id))
            .OrderByDescending(turn => turn.CreatedUtc)
            .ThenByDescending(turn => turn.Id, StringComparer.Ordinal)
            .FirstOrDefault()?.Id ?? "";
    }

    string ResolveNearestSurvivingParent(string turnId, HashSet<string> turnIds)
    {
        if (Document is null)
            return "";

        var byId = Document.Transcript.Turns.ToDictionary(turn => turn.Id, StringComparer.Ordinal);
        var currentId = turnId;
        while (!string.IsNullOrWhiteSpace(currentId) && byId.TryGetValue(currentId, out var turn))
        {
            if (!turnIds.Contains(currentId))
                return currentId;

            currentId = turn.ParentTurnId;
        }

        return "";
    }

    SnapshotDeleteImpact BuildSnapshotDeleteImpact(RpTranscriptSnapshot snapshot)
    {
        if (Document is null)
            return new(snapshot.Id, 0, 0, false, false, 0, 0);

        var activeSnapshots = SnapshotsForActivePath();
        var snapshotIndex = activeSnapshots.ToList().FindIndex(candidate => string.Equals(candidate.Id, snapshot.Id, StringComparison.Ordinal));
        var coveredTurnIds = Document.Transcript.Turns
            .Where(turn => string.Equals(turn.SnapshotId, snapshot.Id, StringComparison.Ordinal))
            .Select(turn => turn.Id)
            .ToHashSet(StringComparer.Ordinal);
        var coveredBranchCount = Document.Transcript.Turns
            .Count(turn => coveredTurnIds.Contains(turn.ParentTurnId) && !coveredTurnIds.Contains(turn.Id));

        return new(
            snapshot.Id,
            coveredTurnIds.Count,
            Document.Timeline.Count(entry => string.Equals(entry.SnapshotId, snapshot.Id, StringComparison.Ordinal)),
            snapshotIndex >= 0,
            snapshotIndex >= 0 && snapshotIndex == activeSnapshots.Count - 1,
            snapshotIndex >= 0 ? activeSnapshots.Count - snapshotIndex - 1 : 0,
            coveredBranchCount);
    }

    static Dictionary<string, string> BuildSnapshotPrivateIntents(RpTranscriptSnapshot? latestSnapshot, IEnumerable<RpTranscriptTurn> coveredTurns)
    {
        var privateIntents = latestSnapshot?.PrivateIntentByCharacterId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var turn in coveredTurns)
        {
            foreach (var pair in turn.PrivateIntentByCharacterId.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
                privateIntents[pair.Key] = pair.Value;
        }

        return privateIntents;
    }

    static Dictionary<string, string> BuildSnapshotAppearances(RpTranscriptSnapshot? latestSnapshot, IEnumerable<RpTranscriptTurn> coveredTurns)
    {
        var appearances = latestSnapshot?.CharacterAppearances.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var turn in coveredTurns)
        {
            foreach (var pair in turn.AppearanceByCharacterId.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)))
                appearances[pair.Key] = pair.Value;
        }

        return appearances;
    }

    static RpSnapshotDraftTurn ToDraftTurn(RpTranscriptTurn turn) => new()
    {
        Id = turn.Id,
        TurnNumber = turn.TurnNumber,
        SpeakerName = string.IsNullOrWhiteSpace(turn.AuthorName) ? "Narrator" : turn.AuthorName,
        CreatedUtc = turn.CreatedUtc,
        Body = turn.Body
    };

    static RpTranscriptSnapshotTimelineEntry CloneSnapshotTimelineEntry(RpTranscriptSnapshotTimelineEntry value) => new()
    {
        TurnNumber = value.TurnNumber,
        Title = value.Title,
        Description = value.Description,
        CharacterIds = [.. value.CharacterIds],
        LocationIds = [.. value.LocationIds],
        ItemNames = [.. value.ItemNames]
    };

    static RpTranscriptSnapshotRelationshipUpdate CloneSnapshotRelationshipUpdate(RpTranscriptSnapshotRelationshipUpdate value) => new()
    {
        RelationshipId = value.RelationshipId,
        ApplyChange = value.ApplyChange,
        SourceCharacterId = value.SourceCharacterId,
        TargetCharacterId = value.TargetCharacterId,
        RelationshipTypes = [.. value.RelationshipTypes],
        PrivateTensions = [.. value.PrivateTensions],
        HowSourceSeesTarget = value.HowSourceSeesTarget,
        HowTargetSeesSource = value.HowTargetSeesSource,
        PublicDynamic = value.PublicDynamic,
        Reason = value.Reason,
        EvidenceTurnNumbers = [.. value.EvidenceTurnNumbers]
    };
}

sealed record SnapshotPath(
    IReadOnlyList<RpTranscriptTurn> ActivePath,
    RpTranscriptTurn TargetTurn,
    RpTranscriptSnapshot? LatestSnapshot,
    IReadOnlyList<RpTranscriptTurn> CoveredTurns)
{
    public const int MinimumCoveredTurns = 5;
    public const int MinimumEligibleCompletedTurns = MinimumCoveredTurns + 1;

    public static SnapshotPath Build(RpChatDocument document, string turnId, string failurePrefix = "Creating a snapshot draft failed")
    {
        TranscriptTurnNumbering.EnsureTurnNumbers(document.Transcript);
        var activePath = TranscriptGraph.GetActivePath(document.Transcript);
        var target = ResolveDraftTarget(document, turnId);
        if (target is null)
            throw new InvalidOperationException($"{failurePrefix} because the selected turn is not on the active branch.");
        if (!target.CanCreate)
            throw new InvalidOperationException($"{failurePrefix} because {target.DisabledReason}");

        var targetIndex = activePath.FindIndex(turn => turn.Id == target.TargetTurnId);
        if (targetIndex < 0)
            throw new InvalidOperationException("Creating a snapshot draft failed because the selected turn is not on the active branch.");

        var pathThroughTarget = activePath.Take(targetIndex + 1).ToList();
        var latestSnapshot = GetSnapshotsOnPath(document.Transcript, pathThroughTarget).LastOrDefault();
        var latestSnapshotIndex = latestSnapshot is null
            ? -1
            : pathThroughTarget.FindIndex(turn => turn.Id == latestSnapshot.TurnId);
        var coveredTurns = pathThroughTarget.Skip(latestSnapshotIndex + 1).ToList();
        return new(activePath, pathThroughTarget[targetIndex], latestSnapshot, coveredTurns);
    }

    public static SnapshotDraftTarget? ResolveDraftTarget(RpChatDocument document, string turnId)
    {
        TranscriptTurnNumbering.EnsureTurnNumbers(document.Transcript);
        var activePath = TranscriptGraph.GetActivePath(document.Transcript);
        var requestedIndex = activePath.FindIndex(turn => turn.Id == turnId);
        if (requestedIndex < 0)
            return null;

        var snapshots = GetSnapshotsOnPath(document.Transcript, activePath);
        var latestSnapshot = snapshots.LastOrDefault();
        var latestSnapshotIndex = latestSnapshot is null
            ? -1
            : activePath.FindIndex(turn => string.Equals(turn.Id, latestSnapshot.TurnId, StringComparison.Ordinal));
        var unsnapshottedTurnCount = activePath.Count - latestSnapshotIndex - 1;
        var targetIndex = requestedIndex == activePath.Count - 1
            ? requestedIndex - 1
            : requestedIndex;
        var targetTurnId = targetIndex >= 0 && targetIndex < activePath.Count
            ? activePath[targetIndex].Id
            : turnId;
        var coveredTurnCount = Math.Max(0, targetIndex - latestSnapshotIndex);
        var disabledReason = DisabledReasonFor(targetIndex, latestSnapshotIndex, coveredTurnCount, unsnapshottedTurnCount);
        return new(turnId, targetTurnId, coveredTurnCount, unsnapshottedTurnCount, disabledReason);
    }

    public static IReadOnlyList<RpTranscriptSnapshot> GetSnapshotsOnPath(RpTranscriptState transcript, IReadOnlyList<RpTranscriptTurn> path)
    {
        var pathIndexes = path
            .Select((turn, index) => (turn.Id, index))
            .ToDictionary(pair => pair.Id, pair => pair.index, StringComparer.Ordinal);
        return transcript.Snapshots
            .Where(snapshot => pathIndexes.ContainsKey(snapshot.TurnId))
            .OrderBy(snapshot => pathIndexes[snapshot.TurnId])
            .ThenBy(snapshot => snapshot.CreatedUtc)
            .ToList();
    }

    static string DisabledReasonFor(int targetIndex, int latestSnapshotIndex, int coveredTurnCount, int unsnapshottedTurnCount)
    {
        if (unsnapshottedTurnCount < MinimumEligibleCompletedTurns)
            return $"at least {MinimumEligibleCompletedTurns} completed messages are needed so the latest message remains live.";
        if (targetIndex <= latestSnapshotIndex)
            return "the selected message is already covered by the latest snapshot.";
        if (coveredTurnCount < MinimumCoveredTurns)
            return $"a snapshot must cover at least {MinimumCoveredTurns} completed messages.";

        return "";
    }
}
