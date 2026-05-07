using System.Text.Json;
using AgentRp.Data;
using AgentRp.Models;
using AgentRp.Serialization;

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

    static T Deserialize<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

        try
        {
            return JsonSerializer.Deserialize<T>(json, AppJsonSerializerOptions.Web) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
