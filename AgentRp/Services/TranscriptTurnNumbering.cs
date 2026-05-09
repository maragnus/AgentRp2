using AgentRp.Models;

namespace AgentRp.Services;

public static class TranscriptTurnNumbering
{
    public static string Format(int turnNumber) =>
        turnNumber > 0 ? $"Turn {turnNumber}" : "Turn ?";

    public static string Format(RpTranscriptTurn turn) =>
        Format(turn.TurnNumber);

    public static string Format(RpSnapshotDraftTurn turn) =>
        Format(turn.TurnNumber);

    public static string FormatTranscriptStart(IReadOnlyList<RpTranscriptTurn> turns)
    {
        var first = turns.FirstOrDefault(turn => !string.IsNullOrWhiteSpace(turn.Body)) ?? turns.FirstOrDefault();
        return first is null ? "" : $"Transcript starts at {Format(first)}.";
    }

    public static int NextTurnNumber(RpTranscriptState transcript, string parentTurnId)
    {
        EnsureTurnNumbers(transcript);
        if (string.IsNullOrWhiteSpace(parentTurnId))
            return 1;

        var parent = transcript.Turns.FirstOrDefault(turn => string.Equals(turn.Id, parentTurnId, StringComparison.Ordinal));
        return parent is null ? 1 : parent.TurnNumber + 1;
    }

    public static void EnsureTurnNumbers(RpTranscriptState transcript)
    {
        var byId = transcript.Turns.ToDictionary(turn => turn.Id, StringComparer.Ordinal);
        foreach (var turn in transcript.Turns)
            if (turn.TurnNumber <= 0)
                turn.TurnNumber = ResolveTurnNumber(turn, byId, []);
    }

    static int ResolveTurnNumber(
        RpTranscriptTurn turn,
        IReadOnlyDictionary<string, RpTranscriptTurn> byId,
        HashSet<string> resolving)
    {
        if (turn.TurnNumber > 0)
            return turn.TurnNumber;

        if (string.IsNullOrWhiteSpace(turn.ParentTurnId))
            return 1;

        if (!resolving.Add(turn.Id))
            return 1;

        var value = byId.TryGetValue(turn.ParentTurnId, out var parent)
            ? ResolveTurnNumber(parent, byId, resolving) + 1
            : 1;
        resolving.Remove(turn.Id);
        return value;
    }
}
