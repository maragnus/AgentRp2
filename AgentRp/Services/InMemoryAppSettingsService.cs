namespace AgentRp.Services;

public sealed class InMemoryAppSettingsService : IAppSettingsService
{
    readonly object _gate = new();
    readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public Task<T> GetAsync<T>(string key, T fallback, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult(_values.TryGetValue(key, out var value) && value is T typed ? typed : fallback);
    }

    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _values[key] = value;

        return Task.CompletedTask;
    }
}
