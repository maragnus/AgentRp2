using AgentRp.Models;
using AgentRp.Session;

namespace AgentRp.Components.Providers;

public sealed class ProviderConnectionDraft
{
    public string ProviderId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ManagementApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string TeamId { get; set; } = "";

    public static ProviderConnectionDraft FromProvider(AiProvider provider) => new()
    {
        ProviderId = provider.Id,
        Name = provider.Name,
        ApiKey = provider.ApiKey,
        ManagementApiKey = provider.ManagementApiKey,
        Endpoint = provider.Endpoint,
        AccountId = provider.AccountId,
        ProjectId = provider.ProjectId,
        TeamId = provider.TeamId
    };

    public bool Matches(AiProvider provider) =>
        string.Equals(Name, provider.Name, StringComparison.Ordinal)
        && string.Equals(ApiKey, provider.ApiKey, StringComparison.Ordinal)
        && string.Equals(ManagementApiKey, provider.ManagementApiKey, StringComparison.Ordinal)
        && string.Equals(Endpoint, provider.Endpoint, StringComparison.Ordinal)
        && string.Equals(AccountId, provider.AccountId, StringComparison.Ordinal)
        && string.Equals(ProjectId, provider.ProjectId, StringComparison.Ordinal)
        && string.Equals(TeamId, provider.TeamId, StringComparison.Ordinal);

    public void ApplyTo(AiProvider provider)
    {
        provider.Name = Name;
        provider.ApiKey = ApiKey;
        provider.ManagementApiKey = ManagementApiKey;
        provider.Endpoint = Endpoint;
        provider.AccountId = AccountId;
        provider.ProjectId = ProjectId;
        provider.TeamId = TeamId;
    }

    public AiProvider CloneProvider(AiProvider provider) => new()
    {
        Id = provider.Id,
        Name = Name,
        Type = provider.Type,
        Enabled = provider.Enabled,
        ApiKey = ApiKey,
        ManagementApiKey = ManagementApiKey,
        Endpoint = Endpoint,
        AccountId = AccountId,
        ProjectId = ProjectId,
        TeamId = TeamId,
        LastMetricsRefreshUtc = provider.LastMetricsRefreshUtc,
        LastMetricsError = provider.LastMetricsError,
        Models = provider.Models.Select(SessionCloner.Clone).ToList(),
        Metrics = provider.Metrics.Select(SessionCloner.Clone).ToList()
    };
}

public sealed record ProviderConnectionValidation(bool IsValid, string Message)
{
    public static ProviderConnectionValidation Valid { get; } = new(true, "");

    public static ProviderConnectionValidation Validate(AiProviderMeta meta, ProviderConnectionDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Name))
            return new(false, "Display name is required.");

        if (meta.ApiKeyRequired && string.IsNullOrWhiteSpace(draft.ApiKey))
            return new(false, $"{meta.KeyLabel} is required.");

        if (meta.EndpointRequired && string.IsNullOrWhiteSpace(draft.Endpoint))
            return new(false, "Endpoint URL is required.");

        if (!string.IsNullOrWhiteSpace(draft.Endpoint)
            && !draft.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !draft.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return new(false, "Endpoint URL must start with http:// or https://.");

        return Valid;
    }
}
