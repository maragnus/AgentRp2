using System.Linq.Expressions;
using AgentRp.Data;
using AgentRp.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Session;

internal static class StoryCardChildRowPersistence
{
    public static async Task SaveAsync<TModel, TRow>(
        DbSet<TRow> rows,
        Expression<Func<TRow, bool>> ownerPredicate,
        IReadOnlyList<TModel> models,
        Action<TModel, TRow, int> apply,
        CancellationToken cancellationToken)
        where TModel : IStoryCardChildModel
        where TRow : class, IStoryCardChildRow, new()
    {
        var existing = await rows
            .Where(ownerPredicate)
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var desiredIds = models.Select(model => model.Id).ToHashSet(StringComparer.Ordinal);
        rows.RemoveRange(existing.Values.Where(row => !desiredIds.Contains(row.Id)));

        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index];
            if (!existing.TryGetValue(model.Id, out var row))
            {
                row = new();
                rows.Add(row);
            }
            apply(model, row, index);
        }
    }
}
