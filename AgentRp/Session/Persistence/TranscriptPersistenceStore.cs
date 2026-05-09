using AgentRp.Data;
using AgentRp.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

internal static class TranscriptPersistenceStore
{
    public static async Task<RpTranscriptState> LoadActiveAsync(
        IDbContextFactory<RpDbContext> dbContextFactory,
        string chatId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var shellJson = await dbContext.ChatDocuments
            .AsNoTracking()
            .Where(x => x.ChatId == chatId)
            .Select(x => x.MessagesJson)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadActiveAsync(dbContext, chatId, shellJson, cancellationToken);
    }

    public static async Task<RpTranscriptState> LoadActiveAsync(
        RpDbContext dbContext,
        string chatId,
        string? transcriptJson,
        CancellationToken cancellationToken)
    {
        var state = PersistenceJson.Deserialize(transcriptJson, new RpTranscriptState());

        if (string.IsNullOrWhiteSpace(state.ActiveLeafTurnId))
            return state;

        var nodeRows = await TranscriptDisplayPathQuery.LoadAsync(dbContext, chatId, state.ActiveLeafTurnId, cancellationToken);
        var pathTurnIds = nodeRows
            .Where(row => row.NodeKind == TranscriptDisplayNodeKinds.Turn)
            .Select(row => row.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var snapshotIds = nodeRows
            .Where(row => row.NodeKind == TranscriptDisplayNodeKinds.Snapshot)
            .Select(row => row.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var pathParentIds = pathTurnIds
            .Concat(snapshotIds)
            .ToHashSet(StringComparer.Ordinal);

        var snapshots = await dbContext.TranscriptSnapshots
            .AsNoTracking()
            .Where(x => x.ChatId == chatId
                && (snapshotIds.Contains(x.Id)
                    || snapshotIds.Contains(x.ConsumedBySnapshotId)))
            .OrderBy(x => x.CreatedUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var snapshotEndTurnIds = snapshots
            .Select(snapshot => snapshot.EndTurnId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var turns = await dbContext.TranscriptTurns
            .AsNoTracking()
            .Where(x => x.ChatId == chatId
                && (pathTurnIds.Contains(x.Id)
                    || snapshotEndTurnIds.Contains(x.Id)
                    || pathParentIds.Contains(x.ParentTurnId)))
            .OrderBy(x => x.CreatedUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        state.Turns = turns.Select(TranscriptPersistenceMapper.ToModel).ToList();
        state.Snapshots = snapshots.Select(TranscriptPersistenceMapper.ToModel).ToList();
        state.IsPartial = true;
        return state;
    }

    public static async Task SaveRowsAsync(
        RpDbContext dbContext,
        RpChatDocument document,
        CancellationToken cancellationToken)
    {
        var chatId = document.Chat.Id;
        await SaveTurnRowsAsync(dbContext, document, chatId, cancellationToken);
        await SaveSnapshotRowsAsync(dbContext, document, chatId, cancellationToken);
        await SaveCurrentSceneCharacterRowsAsync(dbContext, document, chatId, cancellationToken);
    }

    static async Task SaveTurnRowsAsync(
        RpDbContext dbContext,
        RpChatDocument document,
        string chatId,
        CancellationToken cancellationToken)
    {
        var existingTurns = await dbContext.TranscriptTurns
            .Where(x => x.ChatId == chatId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var desiredTurnIds = document.Transcript.Turns.Select(turn => turn.Id).ToHashSet(StringComparer.Ordinal);
        dbContext.TranscriptTurns.RemoveRange(existingTurns.Values.Where(row => document.Transcript.DeletedTurnIds.Contains(row.Id)));
        if (!document.Transcript.IsPartial)
            dbContext.TranscriptTurns.RemoveRange(existingTurns.Values.Where(row => !desiredTurnIds.Contains(row.Id)));

        foreach (var turn in document.Transcript.Turns)
        {
            if (!existingTurns.TryGetValue(turn.Id, out var row))
            {
                row = new() { ChatId = chatId, Id = turn.Id };
                dbContext.TranscriptTurns.Add(row);
            }

            TranscriptPersistenceMapper.Apply(turn, row);
        }
    }

    static async Task SaveSnapshotRowsAsync(
        RpDbContext dbContext,
        RpChatDocument document,
        string chatId,
        CancellationToken cancellationToken)
    {
        var existingSnapshots = await dbContext.TranscriptSnapshots
            .Where(x => x.ChatId == chatId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var desiredSnapshotIds = document.Transcript.Snapshots.Select(snapshot => snapshot.Id).ToHashSet(StringComparer.Ordinal);
        dbContext.TranscriptSnapshots.RemoveRange(existingSnapshots.Values.Where(row => document.Transcript.DeletedSnapshotIds.Contains(row.Id)));
        if (!document.Transcript.IsPartial)
            dbContext.TranscriptSnapshots.RemoveRange(existingSnapshots.Values.Where(row => !desiredSnapshotIds.Contains(row.Id)));

        foreach (var snapshot in document.Transcript.Snapshots)
        {
            if (!existingSnapshots.TryGetValue(snapshot.Id, out var row))
            {
                row = new() { ChatId = chatId, Id = snapshot.Id };
                dbContext.TranscriptSnapshots.Add(row);
            }

            TranscriptPersistenceMapper.Apply(snapshot, row);
        }
    }

    static async Task SaveCurrentSceneCharacterRowsAsync(
        RpDbContext dbContext,
        RpChatDocument document,
        string chatId,
        CancellationToken cancellationToken)
    {
        var existingSceneCharacters = await dbContext.ChatCurrentSceneCharacters
            .Where(x => x.ChatId == chatId)
            .ToDictionaryAsync(x => x.CharacterId, cancellationToken);
        var currentScene = TranscriptGraph.GetVisibleScene(document.Transcript);
        var sceneCharacterIds = currentScene.InSceneCharacterIds.ToHashSet(StringComparer.Ordinal);
        dbContext.ChatCurrentSceneCharacters.RemoveRange(existingSceneCharacters.Values.Where(row => !sceneCharacterIds.Contains(row.CharacterId)));
        for (var index = 0; index < currentScene.InSceneCharacterIds.Count; index++)
        {
            var characterId = currentScene.InSceneCharacterIds[index];
            if (!existingSceneCharacters.TryGetValue(characterId, out var row))
            {
                row = new() { ChatId = chatId, CharacterId = characterId };
                dbContext.ChatCurrentSceneCharacters.Add(row);
            }

            row.SortOrder = index;
        }
    }
}
