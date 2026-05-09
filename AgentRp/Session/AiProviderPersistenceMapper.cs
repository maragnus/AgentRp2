using AgentRp.Data;
using AgentRp.Models;

namespace AgentRp.Session;

internal static class AiProviderPersistenceMapper
{
    public static AiProvider ToModel(AiProviderRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Type = row.Type,
        Enabled = row.Enabled,
        ApiKey = row.ApiKey,
        ManagementApiKey = row.ManagementApiKey,
        Endpoint = row.Endpoint,
        AccountId = row.AccountId,
        ProjectId = row.ProjectId,
        TeamId = row.TeamId,
        LastMetricsRefreshUtc = row.LastMetricsRefreshUtc,
        LastMetricsError = row.LastMetricsError,
        Models = row.Models
            .OrderBy(model => model.SortOrder)
            .ThenBy(model => model.Id)
            .Select(ToModel)
            .ToList(),
        Metrics = row.Metrics
            .OrderBy(metric => metric.Label)
            .ThenBy(metric => metric.Kind)
            .Select(ToModel)
            .ToList()
    };

    static AiProviderModel ToModel(AiProviderModelRow row) => new()
    {
        Id = row.Id,
        DisplayName = row.DisplayName,
        Endpoint = row.Endpoint,
        Repository = row.Repository,
        CreatedUnix = row.CreatedUnix,
        Enabled = row.Enabled,
        Roles = Deserialize(row.RolesJson, new HashSet<AiModelRole>()),
        LastVoiceRefreshUtc = row.LastVoiceRefreshUtc,
        LastVoiceRefreshError = row.LastVoiceRefreshError,
        Voices = Deserialize(row.VoicesJson, new List<AiProviderVoice>())
    };

    static AiProviderMetric ToModel(AiProviderMetricRow row) => new()
    {
        Id = row.Id,
        Kind = row.Kind,
        Label = row.Label,
        Value = row.Value,
        Detail = row.Detail,
        RefreshedUtc = row.RefreshedUtc
    };

    public static AiProviderRow ToRow(AiProvider provider, int sortOrder, DateTime now) => new()
    {
        Id = provider.Id,
        Name = provider.Name,
        Type = provider.Type,
        Enabled = provider.Enabled,
        ApiKey = provider.ApiKey,
        ManagementApiKey = provider.ManagementApiKey,
        Endpoint = provider.Endpoint,
        AccountId = provider.AccountId,
        ProjectId = provider.ProjectId,
        TeamId = provider.TeamId,
        LastMetricsRefreshUtc = provider.LastMetricsRefreshUtc,
        LastMetricsError = provider.LastMetricsError,
        SortOrder = sortOrder,
        CreatedUtc = now,
        UpdatedUtc = now,
        Models = provider.Models.Select(ToRow).ToList(),
        Metrics = provider.Metrics.Select(ToRow).ToList()
    };

    static AiProviderModelRow ToRow(AiProviderModel model, int sortOrder) => new()
    {
        Id = model.Id,
        DisplayName = model.DisplayName,
        Endpoint = model.Endpoint,
        Repository = model.Repository,
        CreatedUnix = model.CreatedUnix,
        Enabled = model.Enabled,
        RolesJson = PersistenceJson.Serialize(model.Roles),
        LastVoiceRefreshUtc = model.LastVoiceRefreshUtc,
        LastVoiceRefreshError = model.LastVoiceRefreshError,
        VoicesJson = PersistenceJson.Serialize(model.Voices),
        SortOrder = sortOrder
    };

    static AiProviderMetricRow ToRow(AiProviderMetric metric) => new()
    {
        Id = string.IsNullOrWhiteSpace(metric.Id) ? $"pm{Guid.NewGuid():N}" : metric.Id,
        Kind = metric.Kind,
        Label = metric.Label,
        Value = metric.Value,
        Detail = metric.Detail,
        RefreshedUtc = metric.RefreshedUtc
    };

    static T Deserialize<T>(string? json, T fallback) => PersistenceJson.Deserialize(json, fallback);
}
