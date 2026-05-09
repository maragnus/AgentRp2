namespace AgentRp.Data;

public sealed class AiProviderRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string ManagementApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string TeamId { get; set; } = "";
    public DateTime? LastMetricsRefreshUtc { get; set; }
    public string LastMetricsError { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<AiProviderModelRow> Models { get; set; } = [];
    public List<AiProviderMetricRow> Metrics { get; set; } = [];
}

public sealed class AiProviderModelRow
{
    public string ProviderId { get; set; } = "";
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Repository { get; set; } = "";
    public long? CreatedUnix { get; set; }
    public bool Enabled { get; set; }
    public string RolesJson { get; set; } = "[]";
    public DateTime? LastVoiceRefreshUtc { get; set; }
    public string LastVoiceRefreshError { get; set; } = "";
    public string VoicesJson { get; set; } = "[]";
    public int SortOrder { get; set; }
    public AiProviderRow Provider { get; set; } = null!;
}

public sealed class AiProviderMetricRow
{
    public string Id { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime RefreshedUtc { get; set; }
    public AiProviderRow Provider { get; set; } = null!;
}
