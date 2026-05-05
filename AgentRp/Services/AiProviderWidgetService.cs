using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentRp.Models;
using AgentRp.Serialization;

namespace AgentRp.Services;

public enum ManagedEndpointAction
{
    Start,
    Pause,
    ScaleToZero
}

public sealed record ManagedEndpointStatusView(
    string ProviderId,
    string ModelId,
    string AgentName,
    bool IsLinked,
    string? State,
    string? StatusMessage,
    DateTime? StatusUpdatedUtc,
    string? Repository,
    string? Namespace,
    string? EndpointName,
    bool CanStart,
    bool CanPause,
    bool CanScaleToZero,
    string? UnlinkedReason);

public interface IAiProviderWidgetService
{
    Task<IReadOnlyList<AiProviderMetric>> RefreshMetricsAsync(AiProvider provider, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManagedEndpointStatusView>> GetHuggingFaceStatusesAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default);
    Task<ManagedEndpointStatusView> ExecuteHuggingFaceActionAsync(AiProvider provider, AiProviderModel model, ManagedEndpointAction action, CancellationToken cancellationToken = default);
}

public sealed class AiProviderWidgetService(IHttpClientFactory httpClientFactory) : IAiProviderWidgetService
{
    static readonly Uri HuggingFaceWhoAmIUri = new("https://huggingface.co/api/whoami-v2");
    static readonly Uri HuggingFaceManagementBaseUri = new("https://api.endpoints.huggingface.cloud/v2/");
    readonly ConcurrentDictionary<string, Task<IReadOnlyList<string>>> _namespaceCache = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<AiProviderMetric>> RefreshMetricsAsync(AiProvider provider, CancellationToken cancellationToken = default)
    {
        var metrics = new List<AiProviderMetric>
        {
            CreateMetric("connection", "Connection", provider.Enabled ? "Ready" : "Disabled", provider.Enabled ? "" : "This provider is disabled.")
        };

        if (provider.Models.Count > 0)
            metrics.Add(CreateMetric("models", "Models", $"{provider.Models.Count} discovered", $"{provider.Models.Count(model => model.Enabled)} enabled for use"));

        if (provider.Type == "claude" && string.IsNullOrWhiteSpace(provider.ManagementApiKey))
        {
            metrics.Add(CreateMetric("usage", "Usage", "Admin key required", "Anthropic usage and cost metrics require an Admin API key."));
            return metrics;
        }

        if (provider.Type == "grok" && string.IsNullOrWhiteSpace(provider.TeamId))
        {
            metrics.Add(CreateMetric("billing", "Billing", "Team ID required", "xAI billing metrics require the team id from xAI Console."));
            return metrics;
        }

        if (provider.Type == "huggingface")
        {
            metrics.Add(CreateMetric("billing", "Billing", "Endpoint runtime based", "Hugging Face Inference Endpoints are billed by deployed runtime; pause endpoints to stop billing."));
            return metrics;
        }

        await TryLoadRemoteMetricsAsync(provider, metrics, cancellationToken);
        return metrics;
    }

    public async Task<IReadOnlyList<ManagedEndpointStatusView>> GetHuggingFaceStatusesAsync(IReadOnlyList<AiProvider> providers, CancellationToken cancellationToken = default)
    {
        var models = providers
            .Where(provider => provider.Type == "huggingface")
            .SelectMany(provider => provider.Models.Select(model => new { Provider = provider, Model = model }))
            .ToList();
        if (models.Count == 0)
            return [];

        var endpointsByToken = new Dictionary<string, IReadOnlyDictionary<string, ManagedEndpoint>>(StringComparer.Ordinal);
        var authenticationFailedTokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in models.GroupBy(item => item.Provider.ApiKey, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(group.Key) || endpointsByToken.ContainsKey(group.Key) || authenticationFailedTokens.Contains(group.Key))
                continue;

            try
            {
                endpointsByToken[group.Key] = await GetEndpointsByUrlAsync(group.Key, cancellationToken);
            }
            catch (HuggingFaceAuthenticationException)
            {
                authenticationFailedTokens.Add(group.Key);
            }
        }

        var statuses = new List<ManagedEndpointStatusView>(models.Count);
        foreach (var item in models)
        {
            if (string.IsNullOrWhiteSpace(item.Provider.ApiKey))
            {
                statuses.Add(BuildMissingApiKeyStatus(item.Provider, item.Model));
                continue;
            }

            if (authenticationFailedTokens.Contains(item.Provider.ApiKey))
            {
                statuses.Add(BuildAuthenticationFailedStatus(item.Provider, item.Model));
                continue;
            }

            statuses.Add(BuildStatus(item.Provider, item.Model, endpointsByToken[item.Provider.ApiKey]));
        }

        return statuses;
    }

    public async Task<ManagedEndpointStatusView> ExecuteHuggingFaceActionAsync(AiProvider provider, AiProviderModel model, ManagedEndpointAction action, CancellationToken cancellationToken = default)
    {
        if (provider.Type != "huggingface")
            throw new InvalidOperationException($"Managing the Hugging Face endpoint failed because {provider.Name} is not a Hugging Face provider.");
        if (string.IsNullOrWhiteSpace(provider.ApiKey))
            throw new InvalidOperationException($"Managing the Hugging Face endpoint for '{DisplayName(model)}' failed because the configured API key is missing.");

        try
        {
            var endpoint = await GetLinkedEndpointAsync(provider, model, cancellationToken);
            ValidateAction(DisplayName(model), action, endpoint);

            var relativePath = action switch
            {
                ManagedEndpointAction.Start => $"endpoint/{Uri.EscapeDataString(endpoint.Namespace)}/{Uri.EscapeDataString(endpoint.Endpoint.Name)}/resume",
                ManagedEndpointAction.Pause => $"endpoint/{Uri.EscapeDataString(endpoint.Namespace)}/{Uri.EscapeDataString(endpoint.Endpoint.Name)}/pause",
                ManagedEndpointAction.ScaleToZero => $"endpoint/{Uri.EscapeDataString(endpoint.Namespace)}/{Uri.EscapeDataString(endpoint.Endpoint.Name)}/scale-to-zero",
                _ => throw new InvalidOperationException($"Managing the Hugging Face endpoint for '{DisplayName(model)}' failed because the action '{action}' is not supported.")
            };

            using var client = CreateHuggingFaceClient(provider.ApiKey);
            using var response = await client.PostAsync(new Uri(HuggingFaceManagementBaseUri, relativePath), content: null, cancellationToken);
            var updatedEndpoint = await ReadJsonResponseAsync<EndpointWithStatusResponse>(response, cancellationToken);
            return BuildLinkedStatus(provider, model, new(endpoint.Namespace, updatedEndpoint));
        }
        catch (HuggingFaceAuthenticationException exception)
        {
            var message = UserFacingErrorMessageBuilder.BuildExternalHttpFailure(
                $"Managing the Hugging Face endpoint for '{DisplayName(model)}'",
                exception.StatusCode,
                exception.ResponseBody ?? string.Empty,
                "Hugging Face");
            throw new ExternalServiceFailureException(message, exception.StatusCode, exception.ResponseBody, exception);
        }
    }

    async Task TryLoadRemoteMetricsAsync(AiProvider provider, List<AiProviderMetric> metrics, CancellationToken cancellationToken)
    {
        if (provider.Type == "grok" && !string.IsNullOrWhiteSpace(provider.TeamId))
        {
            using var client = CreateBearerClient(string.IsNullOrWhiteSpace(provider.ManagementApiKey) ? provider.ApiKey : provider.ManagementApiKey);
            using var response = await client.GetAsync(new Uri($"https://management-api.x.ai/v1/billing/teams/{Uri.EscapeDataString(provider.TeamId)}/prepaid/balance"), cancellationToken);
            metrics.Add(response.IsSuccessStatusCode
                ? CreateMetric("prepaid-balance", "Prepaid Balance", "Available", await response.Content.ReadAsStringAsync(cancellationToken))
                : CreateMetric("billing", "Billing", "Unavailable", $"xAI returned {(int)response.StatusCode} ({response.StatusCode})."));
        }

        if (provider.Type == "claude" && !string.IsNullOrWhiteSpace(provider.ManagementApiKey))
        {
            var start = DateTime.UtcNow.Date.AddDays(-7).ToString("O");
            var end = DateTime.UtcNow.Date.AddDays(1).ToString("O");
            using var client = CreateApiKeyClient(provider.ManagementApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            var uri = new Uri($"https://api.anthropic.com/v1/organizations/usage_report/messages?starting_at={Uri.EscapeDataString(start)}&ending_at={Uri.EscapeDataString(end)}&bucket_width=1d");
            using var response = await client.GetAsync(uri, cancellationToken);
            metrics.Add(response.IsSuccessStatusCode
                ? CreateMetric("usage", "Usage", "Last 7 days available", await response.Content.ReadAsStringAsync(cancellationToken))
                : CreateMetric("usage", "Usage", "Unavailable", $"Anthropic returned {(int)response.StatusCode} ({response.StatusCode})."));
        }

        if (provider.Type == "openai")
            metrics.Add(CreateMetric("usage", "Usage", "Use dashboard/API", "OpenAI usage and costs are available from the Usage and Costs APIs for projects with permission."));
    }

    async Task<Dictionary<string, ManagedEndpoint>> GetEndpointsByUrlAsync(string apiKey, CancellationToken cancellationToken)
    {
        var namespaces = await GetNamespacesAsync(apiKey, cancellationToken);
        var endpointsByUrl = new Dictionary<string, ManagedEndpoint>(StringComparer.OrdinalIgnoreCase);

        foreach (var @namespace in namespaces)
        {
            var cursor = "";
            var seenCursors = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                var page = await ListEndpointsAsync(apiKey, @namespace, cursor, cancellationToken);
                foreach (var endpoint in page.Items)
                {
                    var normalizedUrl = AgentEndpointUrlNormalizer.Normalize(endpoint.Status.Url);
                    if (normalizedUrl is not null)
                        endpointsByUrl[normalizedUrl] = new(@namespace, endpoint);
                }

                cursor = string.IsNullOrWhiteSpace(page.NextCursor) || !seenCursors.Add(page.NextCursor) ? "" : page.NextCursor;
            }
            while (!string.IsNullOrWhiteSpace(cursor));
        }

        return endpointsByUrl;
    }

    async Task<IReadOnlyList<string>> GetNamespacesAsync(string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            return await _namespaceCache.GetOrAdd(apiKey, key => LoadNamespacesAsync(key, cancellationToken));
        }
        catch
        {
            _namespaceCache.TryRemove(apiKey, out _);
            throw;
        }
    }

    async Task<IReadOnlyList<string>> LoadNamespacesAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var client = CreateHuggingFaceClient(apiKey);
        using var response = await client.GetAsync(HuggingFaceWhoAmIUri, cancellationToken);
        var whoAmI = await ReadJsonResponseAsync<HuggingFaceWhoAmIResponse>(response, cancellationToken);

        var namespaces = new List<string>();
        AddNamespace(namespaces, whoAmI.Name);
        foreach (var organization in whoAmI.Orgs)
            AddNamespace(namespaces, organization.Name);

        return namespaces;
    }

    async Task<EndpointListResponse> ListEndpointsAsync(string apiKey, string @namespace, string? cursor, CancellationToken cancellationToken)
    {
        using var client = CreateHuggingFaceClient(apiKey);
        var relativePath = $"endpoint/{Uri.EscapeDataString(@namespace)}?limit=100";
        if (!string.IsNullOrWhiteSpace(cursor))
            relativePath += $"&cursor={Uri.EscapeDataString(cursor)}";

        using var response = await client.GetAsync(new Uri(HuggingFaceManagementBaseUri, relativePath), cancellationToken);
        return await ReadJsonResponseAsync<EndpointListResponse>(response, cancellationToken);
    }

    async Task<ManagedEndpoint> GetLinkedEndpointAsync(AiProvider provider, AiProviderModel model, CancellationToken cancellationToken)
    {
        var endpointsByUrl = await GetEndpointsByUrlAsync(provider.ApiKey, cancellationToken);
        var normalizedUrl = AgentEndpointUrlNormalizer.Normalize(model.Endpoint);
        if (normalizedUrl is null || !endpointsByUrl.TryGetValue(normalizedUrl, out var endpoint))
            throw new InvalidOperationException($"Managing the Hugging Face endpoint for '{DisplayName(model)}' failed because no matching managed endpoint was found for the configured URL.");

        return endpoint;
    }

    static ManagedEndpointStatusView BuildStatus(AiProvider provider, AiProviderModel model, IReadOnlyDictionary<string, ManagedEndpoint> endpointsByUrl)
    {
        var normalizedUrl = AgentEndpointUrlNormalizer.Normalize(model.Endpoint);
        if (normalizedUrl is null || !endpointsByUrl.TryGetValue(normalizedUrl, out var endpoint))
            return new(provider.Id, model.Id, DisplayName(model), false, null, null, null, null, null, null, false, false, false, "No matching Hugging Face endpoint was found for the configured URL.");

        return BuildLinkedStatus(provider, model, endpoint);
    }

    static ManagedEndpointStatusView BuildLinkedStatus(AiProvider provider, AiProviderModel model, ManagedEndpoint endpoint)
    {
        var state = endpoint.Endpoint.Status.State?.Trim();
        return new(
            provider.Id,
            model.Id,
            DisplayName(model),
            true,
            state,
            string.IsNullOrWhiteSpace(endpoint.Endpoint.Status.Message) ? null : endpoint.Endpoint.Status.Message.Trim(),
            endpoint.Endpoint.Status.UpdatedAt,
            string.IsNullOrWhiteSpace(endpoint.Endpoint.Model.Repository) ? model.Repository : endpoint.Endpoint.Model.Repository.Trim(),
            endpoint.Namespace,
            endpoint.Endpoint.Name,
            CanStart(state),
            CanPause(state),
            CanScaleToZero(state),
            null);
    }

    static ManagedEndpointStatusView BuildMissingApiKeyStatus(AiProvider provider, AiProviderModel model) =>
        new(provider.Id, model.Id, DisplayName(model), false, null, null, null, null, null, null, false, false, false, "Add an API key to link and manage this Hugging Face endpoint.");

    static ManagedEndpointStatusView BuildAuthenticationFailedStatus(AiProvider provider, AiProviderModel model) =>
        new(provider.Id, model.Id, DisplayName(model), false, null, null, null, null, null, null, false, false, false, "The configured API key could not access Hugging Face endpoint management.");

    static void ValidateAction(string agentName, ManagedEndpointAction action, ManagedEndpoint endpoint)
    {
        var state = endpoint.Endpoint.Status.State?.Trim();
        var isAllowed = action switch
        {
            ManagedEndpointAction.Start => CanStart(state),
            ManagedEndpointAction.Pause => CanPause(state),
            ManagedEndpointAction.ScaleToZero => CanScaleToZero(state),
            _ => false
        };
        if (isAllowed)
            return;

        throw new InvalidOperationException($"{DescribeAction(action)} the Hugging Face endpoint for '{agentName}' failed because it is currently '{state ?? "unknown"}'.");
    }

    static bool CanStart(string? state) =>
        state is not null && (state.Equals("paused", StringComparison.OrdinalIgnoreCase) || state.Equals("scaledToZero", StringComparison.OrdinalIgnoreCase));

    static bool CanPause(string? state) => IsActiveState(state);

    static bool CanScaleToZero(string? state) => IsActiveState(state);

    static bool IsActiveState(string? state) =>
        state is not null
        && (state.Equals("running", StringComparison.OrdinalIgnoreCase)
            || state.Equals("pending", StringComparison.OrdinalIgnoreCase)
            || state.Equals("initializing", StringComparison.OrdinalIgnoreCase)
            || state.Equals("updating", StringComparison.OrdinalIgnoreCase));

    static string DescribeAction(ManagedEndpointAction action) =>
        action switch
        {
            ManagedEndpointAction.Start => "Starting",
            ManagedEndpointAction.Pause => "Pausing",
            ManagedEndpointAction.ScaleToZero => "Scaling to zero",
            _ => "Managing"
        };

    static void AddNamespace(List<string> namespaces, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || namespaces.Any(value => string.Equals(value, candidate, StringComparison.Ordinal)))
            return;

        namespaces.Add(candidate);
    }

    HttpClient CreateBearerClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    HttpClient CreateApiKeyClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

        return client;
    }

    HttpClient CreateHuggingFaceClient(string apiKey) => CreateBearerClient(apiKey);

    static async Task<T> ReadJsonResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>(AppJsonSerializerOptions.Web, cancellationToken)
                ?? throw new InvalidOperationException("Reading the Hugging Face endpoint management response failed because the service returned an empty response body.");

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new HuggingFaceAuthenticationException(response.StatusCode, responseBody);

        var message = UserFacingErrorMessageBuilder.BuildExternalHttpFailure(
            "Calling Hugging Face endpoint management",
            response.StatusCode,
            responseBody,
            "Hugging Face");
        throw new ExternalServiceFailureException(message, response.StatusCode, responseBody);
    }

    static AiProviderMetric CreateMetric(string kind, string label, string value, string? detail) => new()
    {
        Id = $"pm{Guid.NewGuid():N}",
        Kind = kind,
        Label = label,
        Value = value,
        Detail = detail ?? "",
        RefreshedUtc = DateTime.UtcNow
    };

    static string DisplayName(AiProviderModel model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName;

    sealed record ManagedEndpoint(string Namespace, EndpointWithStatusResponse Endpoint);

    sealed class HuggingFaceAuthenticationException(HttpStatusCode statusCode, string? responseBody)
        : InvalidOperationException($"Hugging Face endpoint management authentication failed with status code {(int)statusCode} ({statusCode}). Response: {responseBody}")
    {
        public HttpStatusCode StatusCode { get; } = statusCode;

        public string? ResponseBody { get; } = responseBody;
    }

    sealed class HuggingFaceWhoAmIResponse
    {
        public string Name { get; set; } = "";

        public List<HuggingFaceOrganizationResponse> Orgs { get; set; } = [];
    }

    sealed class HuggingFaceOrganizationResponse
    {
        public string Name { get; set; } = "";
    }

    sealed class EndpointListResponse
    {
        public List<EndpointWithStatusResponse> Items { get; set; } = [];

        public string? NextCursor { get; set; }
    }

    sealed class EndpointWithStatusResponse
    {
        public string Name { get; set; } = "";

        public EndpointModelResponse Model { get; set; } = new();

        public EndpointStatusResponse Status { get; set; } = new();
    }

    sealed class EndpointModelResponse
    {
        public string Repository { get; set; } = "";
    }

    sealed class EndpointStatusResponse
    {
        public string? State { get; set; }

        public string? Message { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? Url { get; set; }
    }
}

public static class AgentEndpointUrlNormalizer
{
    public static string? Normalize(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        var trimmedEndpoint = endpoint.Trim();
        if (!trimmedEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !trimmedEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return null;

        var endpointUri = new Uri(trimmedEndpoint);
        var path = endpointUri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            path = path[..^3].TrimEnd('/');

        return $"{endpointUri.Scheme}://{endpointUri.Authority}{path}".TrimEnd('/').ToLowerInvariant();
    }

    public static string NormalizeResponsesEndpoint(string endpoint)
    {
        var trimmedEndpoint = endpoint.Trim();
        if (!trimmedEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !trimmedEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Connecting to Hugging Face failed because the endpoint must start with http:// or https://.");

        var endpointUri = new Uri(trimmedEndpoint);
        var path = endpointUri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            path = $"{path}/v1";

        return $"{endpointUri.Scheme}://{endpointUri.Authority}{path.TrimEnd('/')}/";
    }
}
