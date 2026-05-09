using AgentRp.Data;
using AgentRp.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

internal static class AiProviderPersistenceStore
{
    public static async Task<List<AiProvider>> LoadAsync(
        IDbContextFactory<RpDbContext> dbContextFactory,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.AiProviders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Models)
            .Include(x => x.Metrics)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(AiProviderPersistenceMapper.ToModel).ToList();
    }

    public static async Task SaveAsync(
        IDbContextFactory<RpDbContext> dbContextFactory,
        IReadOnlyList<AiProvider> providers,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        dbContext.AiProviderModels.RemoveRange(dbContext.AiProviderModels);
        dbContext.AiProviders.RemoveRange(dbContext.AiProviders);
        await dbContext.SaveChangesAsync(cancellationToken);

        for (var providerIndex = 0; providerIndex < providers.Count; providerIndex++)
            dbContext.AiProviders.Add(AiProviderPersistenceMapper.ToRow(providers[providerIndex], providerIndex, now));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
