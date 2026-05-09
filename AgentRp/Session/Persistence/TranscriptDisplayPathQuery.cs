using System.Data;
using AgentRp.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

internal static class TranscriptDisplayPathQuery
{
    public static async Task<List<TranscriptDisplayPathRow>> LoadAsync(
        RpDbContext dbContext,
        string chatId,
        string activeLeafTurnId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH DisplayEdges AS (
                    SELECT
                        t.ChatId,
                        t.Id AS CurrentTurnId,
                        COALESCE(NULLIF(s.ParentBeforeStartTurnId, ''), NULLIF(t.ParentTurnId, '')) AS ParentTurnId
                    FROM TranscriptTurns t
                    LEFT JOIN TranscriptSnapshots s
                      ON s.ChatId = t.ChatId
                     AND s.EndTurnId = t.Id
                     AND s.IsActive = CAST(1 AS bit)
                    WHERE t.ChatId = @chatId
                ),
                DisplayPath AS (
                    SELECT
                        CAST(@chatId AS nvarchar(80)) AS ChatId,
                        CAST(@activeLeafTurnId AS nvarchar(80)) AS CurrentTurnId,
                        CAST(0 AS int) AS Depth
                    UNION ALL
                    SELECT
                        p.ChatId,
                        e.ParentTurnId AS CurrentTurnId,
                        p.Depth + 1 AS Depth
                    FROM DisplayPath p
                    INNER JOIN DisplayEdges e
                      ON e.ChatId = p.ChatId
                     AND e.CurrentTurnId = p.CurrentTurnId
                    WHERE p.CurrentTurnId IS NOT NULL
                      AND e.ParentTurnId IS NOT NULL
                )
                SELECT
                    p.Depth,
                    CASE WHEN s.Id IS NULL THEN CAST('turn' AS nvarchar(20)) ELSE CAST('snapshot' AS nvarchar(20)) END AS NodeKind,
                    COALESCE(s.Id, t.Id) AS NodeId
                FROM DisplayPath p
                LEFT JOIN TranscriptSnapshots s
                  ON s.ChatId = p.ChatId
                 AND s.EndTurnId = p.CurrentTurnId
                 AND s.IsActive = CAST(1 AS bit)
                LEFT JOIN TranscriptTurns t
                  ON t.ChatId = p.ChatId
                 AND t.Id = p.CurrentTurnId
                WHERE COALESCE(s.Id, t.Id) IS NOT NULL
                ORDER BY p.Depth DESC
                OPTION (MAXRECURSION 0);
                """;
            var chatIdParameter = command.CreateParameter();
            chatIdParameter.ParameterName = "@chatId";
            chatIdParameter.Value = chatId;
            command.Parameters.Add(chatIdParameter);

            var activeLeafTurnIdParameter = command.CreateParameter();
            activeLeafTurnIdParameter.ParameterName = "@activeLeafTurnId";
            activeLeafTurnIdParameter.Value = activeLeafTurnId;
            command.Parameters.Add(activeLeafTurnIdParameter);

            var rows = new List<TranscriptDisplayPathRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new()
                {
                    Depth = reader.GetInt32(0),
                    NodeKind = reader.GetString(1),
                    NodeId = reader.GetString(2)
                });
            }

            return rows;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}

internal sealed class TranscriptDisplayPathRow
{
    public int Depth { get; set; }
    public string NodeKind { get; set; } = "";
    public string NodeId { get; set; } = "";
}

internal static class TranscriptDisplayNodeKinds
{
    public const string Turn = "turn";
    public const string Snapshot = "snapshot";
}
