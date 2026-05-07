using System.Text.Json;
using AgentRp.Data;
using AgentRp.Serialization;
using Microsoft.EntityFrameworkCore;

namespace AgentRp.Services;

public interface IAppSettingsService
{
    Task<T> GetAsync<T>(string key, T fallback, CancellationToken cancellationToken = default);
    Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}

public sealed class AppSettingsService(IDbContextFactory<RpDbContext> dbContextFactory) : IAppSettingsService
{
    public async Task<T> GetAsync<T>(string key, T fallback, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
        if (string.IsNullOrWhiteSpace(row?.JsonValue))
            return fallback;

        try
        {
            return JsonSerializer.Deserialize<T>(row.JsonValue, AppJsonSerializerOptions.Web) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.AppSettings.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
        if (row is null)
        {
            row = new AppSettingRow { Key = key, CreatedUtc = DateTime.UtcNow };
            dbContext.AppSettings.Add(row);
        }

        row.JsonValue = JsonSerializer.Serialize(value, AppJsonSerializerOptions.Web);
        row.UpdatedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
