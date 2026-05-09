using Microsoft.EntityFrameworkCore;

namespace AgentRp.Data;

public static class DestructiveStoryDatabaseReset
{
    public static async Task ResetStorySchemaIfNeededAsync(RpDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            return;

        if (!await HasLegacyStorySchemaAsync(dbContext, cancellationToken))
            return;

        var providers = await dbContext.AiProviders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(provider => provider.Models)
            .Include(provider => provider.Metrics)
            .OrderBy(provider => provider.SortOrder)
            .ToListAsync(cancellationToken);
        var settings = await dbContext.AppSettings
            .AsNoTracking()
            .OrderBy(setting => setting.Key)
            .ToListAsync(cancellationToken);
        var voices = await dbContext.ElevenLabsVoiceCatalog
            .AsNoTracking()
            .OrderBy(voice => voice.Name)
            .ToListAsync(cancellationToken);
        var voiceStates = await dbContext.ElevenLabsVoiceCatalogStates
            .AsNoTracking()
            .OrderBy(state => state.Id)
            .ToListAsync(cancellationToken);

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        dbContext.AiProviders.AddRange(providers);
        dbContext.AppSettings.AddRange(settings);
        dbContext.ElevenLabsVoiceCatalog.AddRange(voices);
        dbContext.ElevenLabsVoiceCatalogStates.AddRange(voiceStates);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    static async Task<bool> HasLegacyStorySchemaAsync(RpDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[ChatDocuments]', N'U') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is bool result && result;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
