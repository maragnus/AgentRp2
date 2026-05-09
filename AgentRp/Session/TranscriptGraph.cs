using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Session;

static class TranscriptGraph
{
    public const string RootBranchKey = "__root__";

    public static RpSceneFrame GetActiveScene(RpTranscriptState transcript)
    {
        var turn = FindTurn(transcript, transcript.ActiveLeafTurnId);
        return turn?.Scene ?? transcript.RootScene;
    }

    public static RpSceneFrame GetSceneForNextTurn(RpTranscriptState transcript, string parentTurnId)
    {
        if (HasWorkingSceneFor(transcript, parentTurnId))
            return transcript.WorkingScene.Scene;

        var parent = FindTurn(transcript, parentTurnId);
        return parent?.Scene ?? transcript.RootScene;
    }

    public static RpSceneFrame GetVisibleScene(RpTranscriptState transcript) =>
        GetSceneForNextTurn(transcript, transcript.ActiveLeafTurnId);

    public static RpSceneFrame GetEditableWorkingScene(RpTranscriptState transcript)
    {
        if (HasWorkingSceneFor(transcript, transcript.ActiveLeafTurnId))
            return transcript.WorkingScene.Scene;

        transcript.WorkingScene = new()
        {
            IsActive = true,
            ParentTurnId = transcript.ActiveLeafTurnId,
            Scene = SessionCloner.Clone(GetActiveScene(transcript))
        };
        return transcript.WorkingScene.Scene;
    }

    public static void ClearWorkingSceneForParent(RpTranscriptState transcript, string parentTurnId)
    {
        if (HasWorkingSceneFor(transcript, parentTurnId))
            transcript.WorkingScene = new();
    }

    public static void ClearWorkingScene(RpTranscriptState transcript) =>
        transcript.WorkingScene = new();

    static bool HasWorkingSceneFor(RpTranscriptState transcript, string parentTurnId) =>
        transcript.WorkingScene.IsActive
        && string.Equals(transcript.WorkingScene.ParentTurnId, parentTurnId, StringComparison.Ordinal);

    public static RpTranscriptTurn? FindTurn(RpTranscriptState transcript, string turnId) =>
        transcript.Turns.FirstOrDefault(turn => turn.Id == turnId);

    public static RpTranscriptSnapshot? FindSnapshot(RpTranscriptState transcript, string snapshotId) =>
        transcript.Snapshots.FirstOrDefault(snapshot => snapshot.Id == snapshotId);

    public static RpTranscriptSnapshot? FindSnapshotByTurn(RpTranscriptState transcript, string turnId) =>
        transcript.Snapshots.FirstOrDefault(snapshot => snapshot.TurnId == turnId);

    public static List<RpTranscriptTurn> GetChildren(RpTranscriptState transcript, string parentTurnId) =>
        transcript.Turns
            .Where(turn => turn.ParentTurnId == parentTurnId)
            .OrderBy(turn => turn.CreatedUtc)
            .ThenBy(turn => turn.Id, StringComparer.Ordinal)
            .ToList();

    public static List<RpTranscriptTurn> GetSiblings(RpTranscriptState transcript, string turnId)
    {
        var turn = FindTurn(transcript, turnId);
        return turn is null ? [] : GetChildren(transcript, turn.ParentTurnId);
    }

    public static List<RpTranscriptTurn> GetActivePath(RpTranscriptState transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript.ActiveLeafTurnId))
            return [];

        var byId = transcript.Turns.ToDictionary(turn => turn.Id, StringComparer.Ordinal);
        var path = new List<RpTranscriptTurn>();
        var currentId = transcript.ActiveLeafTurnId;
        while (!string.IsNullOrWhiteSpace(currentId) && byId.TryGetValue(currentId, out var turn))
        {
            path.Add(turn);
            currentId = turn.ParentTurnId;
        }

        path.Reverse();
        return path;
    }

    public static HashSet<string> GetActivePathIds(RpTranscriptState transcript) =>
        GetActivePath(transcript)
            .Select(turn => turn.Id)
            .ToHashSet(StringComparer.Ordinal);

    public static RpTranscriptSnapshot? GetLatestSnapshotOnPath(RpTranscriptState transcript, string? leafTurnId = null)
    {
        var pathIds = GetPathIdsToLeaf(transcript, leafTurnId ?? transcript.ActiveLeafTurnId);
        return transcript.Snapshots
            .Where(snapshot => pathIds.Contains(snapshot.TurnId))
            .OrderBy(snapshot => snapshot.CreatedUtc)
            .LastOrDefault();
    }

    public static HashSet<string> GetPathIdsToLeaf(RpTranscriptState transcript, string leafTurnId)
    {
        var byId = transcript.Turns.ToDictionary(turn => turn.Id, StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var currentId = leafTurnId;
        while (!string.IsNullOrWhiteSpace(currentId) && byId.TryGetValue(currentId, out var turn))
        {
            ids.Add(turn.Id);
            currentId = turn.ParentTurnId;
        }

        return ids;
    }

    public static void SelectLeaf(RpTranscriptState transcript, string turnId)
    {
        if (FindTurn(transcript, turnId) is null)
            return;

        transcript.ActiveLeafTurnId = turnId;
        var current = FindTurn(transcript, turnId);
        while (current is not null)
        {
            transcript.BranchSelections[BranchKey(current.ParentTurnId)] = current.Id;
            current = FindTurn(transcript, current.ParentTurnId);
        }
    }

    public static void RepairSelections(RpTranscriptState transcript)
    {
        var existingIds = transcript.Turns.Select(turn => turn.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var key in transcript.BranchSelections.Keys.ToList())
        {
            if (!existingIds.Contains(transcript.BranchSelections[key]))
                transcript.BranchSelections.Remove(key);
        }

        var leaf = FindTurn(transcript, transcript.ActiveLeafTurnId);
        if (leaf is null)
        {
            var fallback = transcript.Turns
                .OrderByDescending(turn => turn.CreatedUtc)
                .ThenByDescending(turn => turn.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            transcript.ActiveLeafTurnId = fallback?.Id ?? "";
        }

        if (!string.IsNullOrWhiteSpace(transcript.ActiveLeafTurnId))
            SelectLeaf(transcript, transcript.ActiveLeafTurnId);
    }

    public static string BranchKey(string parentTurnId) =>
        string.IsNullOrWhiteSpace(parentTurnId) ? RootBranchKey : parentTurnId;
}

static class TranscriptProjector
{
    public static void Apply(RpChatDocument document, DateTime? now = null)
    {
        var transcript = document.Transcript;
        TranscriptTurnNumbering.EnsureTurnNumbers(transcript);
        EnsureSceneDefaults(document);
        var scene = TranscriptGraph.GetVisibleScene(transcript);
        var activePath = TranscriptGraph.GetActivePath(transcript);
        var activeLocationId = scene.LocationId;
        foreach (var location in document.Locations)
            location.IsActive = location.Id == activeLocationId;

        var inSceneCharacters = scene.InSceneCharacterIds.ToHashSet(StringComparer.Ordinal);
        foreach (var character in document.Characters)
            character.InScene = inSceneCharacters.Contains(character.Id);

        var inSceneItems = scene.InSceneItemIds.ToHashSet(StringComparer.Ordinal);
        foreach (var item in document.Items)
            item.InScene = inSceneItems.Contains(item.Id);

        var activeLocation = document.Locations.FirstOrDefault(location => location.Id == activeLocationId)
            ?? document.Locations.FirstOrDefault(location => location.IsActive)
            ?? document.Locations.FirstOrDefault();
        document.Chat.Location = !string.IsNullOrWhiteSpace(scene.LocationName)
            ? scene.LocationName
            : activeLocation?.Name ?? document.Chat.Location;
        document.Chat.Messages = activePath.Count;

        var head = activePath.LastOrDefault();
        if (head is not null)
        {
            document.Chat.Updated = RelativeDateFormatter.FormatDate(head.CreatedUtc, now);
            document.Chat.LastMessageUtc = head.UpdatedUtc == default ? head.CreatedUtc : head.UpdatedUtc;
            document.Chat.LastGeneratedTurnNumber = head.TurnNumber;
        }
        else
        {
            document.Chat.LastMessageUtc = null;
            document.Chat.LastGeneratedTurnNumber = 0;
        }
    }

    static void EnsureSceneDefaults(RpChatDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Transcript.RootScene.LocationId))
        {
            var activeLocation = document.Locations.FirstOrDefault(location => location.IsActive) ?? document.Locations.FirstOrDefault();
            if (activeLocation is not null)
            {
                document.Transcript.RootScene.LocationId = activeLocation.Id;
                document.Transcript.RootScene.LocationName = activeLocation.Name;
            }
        }

        if (document.Transcript.RootScene.InSceneCharacterIds.Count == 0)
        {
            document.Transcript.RootScene.InSceneCharacterIds = document.Characters
                .Where(character => character.InScene)
                .Select(character => character.Id)
                .ToList();
        }

        if (document.Transcript.RootScene.InSceneItemIds.Count == 0)
        {
            document.Transcript.RootScene.InSceneItemIds = document.Items
                .Where(item => item.InScene)
                .Select(item => item.Id)
                .ToList();
        }
    }
}
