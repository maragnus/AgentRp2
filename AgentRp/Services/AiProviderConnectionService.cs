using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;

namespace AgentRp.Services;

public interface IAiProviderConnectionService
{
    Task TestProviderAsync(AiProvider provider, CancellationToken cancellationToken = default);
    Task<List<AiProviderModel>> DiscoverModelsAsync(AiProvider provider, CancellationToken cancellationToken = default);
}

public sealed class AiProviderConnectionService(
    IHttpClientFactory httpClientFactory,
    IModelCapabilityCatalog capabilityCatalog) : IAiProviderConnectionService
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task TestProviderAsync(AiProvider provider, CancellationToken cancellationToken = default)
    {
        var discovered = await DiscoverModelsAsync(provider, cancellationToken);
        if (discovered.Count == 0 && !IsOpenAiCompatible(provider.Type))
            throw new InvalidOperationException($"Testing {provider.Name} failed because the provider did not return any models.");
    }

    public async Task<List<AiProviderModel>> DiscoverModelsAsync(AiProvider provider, CancellationToken cancellationToken = default)
    {
        var discovered = provider.Type switch
        {
            "openai" => await DiscoverOpenAiModelsAsync(provider, cancellationToken),
            "grok" => await DiscoverGrokModelsAsync(provider, cancellationToken),
            "claude" => await DiscoverOpenAiCompatibleModelsAsync(provider, cancellationToken),
            "huggingface" => await DiscoverHuggingFaceModelsAsync(provider, cancellationToken),
            "compatible" => await DiscoverOpenAiCompatibleModelsAsync(provider, cancellationToken),
            _ => []
        };

        discovered = AddKnownProviderModels(provider.Type, discovered);
        return discovered
            .DistinctBy(model => model.Id, StringComparer.Ordinal)
            .OrderByDescending(model => model.CreatedUnix.HasValue)
            .ThenByDescending(model => model.CreatedUnix ?? 0)
            .ThenBy(model => ModelSortRank(provider.Type, model.Id))
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .Select(model => ToProviderModel(provider.Type, model))
            .ToList();
    }

    async Task<List<DiscoveredModel>> DiscoverOpenAiModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "models"), cancellationToken);
        var json = await ReadJsonAsync(response, $"Discovering OpenAI models for '{provider.Name}'", cancellationToken);
        return ReadOpenAiModels(json);
    }

    async Task<List<DiscoveredModel>> DiscoverOpenAiCompatibleModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "models"), cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
            return [];

        var json = await ReadJsonAsync(response, $"Discovering OpenAI-compatible models for '{provider.Name}'", cancellationToken);
        return ReadOpenAiModels(json);
    }

    async Task<List<DiscoveredModel>> DiscoverGrokModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "language-models"), cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken) ?? new JsonObject();
            capabilityCatalog.UpdateLiveGrokCapabilities(json);
            var languageModels = json["models"]?.AsArray().SelectMany(BuildGrokModels).ToList();
            if (languageModels is { Count: > 0 })
                return languageModels;
        }

        using var fallbackResponse = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "models"), cancellationToken);
        var fallbackJson = await ReadJsonAsync(fallbackResponse, $"Discovering Grok models for '{provider.Name}'", cancellationToken);
        return ReadOpenAiModels(fallbackJson);
    }

    static IEnumerable<DiscoveredModel> BuildGrokModels(JsonNode? node)
    {
        var id = node?["id"]?.GetValue<string>();
        var created = ReadCreatedUnix(node);
        if (!string.IsNullOrWhiteSpace(id))
            yield return new(id, CreatedUnix: created);

        foreach (var alias in node?["aliases"]?.AsArray() ?? [])
        {
            var aliasId = alias?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(aliasId))
                yield return new(aliasId, CreatedUnix: created);
        }
    }

    static List<DiscoveredModel> ReadOpenAiModels(JsonNode json) =>
        json["data"]?.AsArray()
            .Select(node => new DiscoveredModel(node?["id"]?.GetValue<string>() ?? "", CreatedUnix: ReadCreatedUnix(node)))
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .ToList()
        ?? [];

    async Task<List<DiscoveredModel>> DiscoverHuggingFaceModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var whoAmIResponse = await client.GetAsync(new Uri("https://huggingface.co/api/whoami-v2"), cancellationToken);
        var whoAmI = await ReadJsonAsync(whoAmIResponse, $"Discovering Hugging Face namespaces for '{provider.Name}'", cancellationToken);
        var namespaces = new List<string>();
        AddNamespace(namespaces, whoAmI["name"]?.GetValue<string>());
        foreach (var org in whoAmI["orgs"]?.AsArray() ?? [])
            AddNamespace(namespaces, org?["name"]?.GetValue<string>());

        var models = new List<DiscoveredModel>();
        foreach (var @namespace in namespaces)
        {
            var cursor = "";
            var seenCursors = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                var path = $"endpoint/{Uri.EscapeDataString(@namespace)}?limit=100";
                if (!string.IsNullOrWhiteSpace(cursor))
                    path += $"&cursor={Uri.EscapeDataString(cursor)}";

                using var response = await client.GetAsync(new Uri(new Uri("https://api.endpoints.huggingface.cloud/v2/"), path), cancellationToken);
                var json = await ReadJsonAsync(response, $"Discovering Hugging Face endpoints for '{provider.Name}'", cancellationToken);
                foreach (var item in json["items"]?.AsArray() ?? [])
                {
                    var endpointName = item?["name"]?.GetValue<string>();
                    var repository = item?["model"]?["repository"]?.GetValue<string>();
                    var endpoint = item?["status"]?["url"]?.GetValue<string>();
                    var modelId = string.IsNullOrWhiteSpace(repository) ? endpointName : repository;
                    if (string.IsNullOrWhiteSpace(modelId))
                        continue;

                    var displayName = string.IsNullOrWhiteSpace(endpointName) || string.Equals(endpointName, modelId, StringComparison.Ordinal)
                        ? modelId
                        : $"{endpointName} ({modelId})";
                    models.Add(new(modelId, displayName, endpoint ?? "", repository ?? ""));
                }

                var nextCursor = json["nextCursor"]?.GetValue<string>();
                cursor = string.IsNullOrWhiteSpace(nextCursor) || !seenCursors.Add(nextCursor) ? "" : nextCursor;
            }
            while (!string.IsNullOrWhiteSpace(cursor));
        }

        return models;
    }

    static void AddNamespace(List<string> namespaces, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || namespaces.Any(value => string.Equals(value, candidate, StringComparison.Ordinal)))
            return;

        namespaces.Add(candidate);
    }

    static long? ReadCreatedUnix(JsonNode? node)
    {
        var value = node?["created"];
        if (value is null)
            return null;

        try
        {
            return value.GetValue<long>();
        }
        catch (InvalidOperationException)
        {
            return long.TryParse(value.GetValue<string>(), out var number) ? number : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    static List<DiscoveredModel> AddKnownProviderModels(string providerType, List<DiscoveredModel> models)
    {
        void Add(string id)
        {
            if (models.All(model => !string.Equals(model.Id, id, StringComparison.Ordinal)))
                models.Add(new(id));
        }

        if (providerType == "openai")
        {
            Add("gpt-5.2");
            Add("gpt-5");
            Add("gpt-5.5");
            Add("gpt-5.5-mini");
            Add("gpt-image-1.5");
            Add("gpt-image-1");
            Add("gpt-image-1-mini");
        }
        else if (providerType == "grok")
        {
            Add("grok-imagine-image");
        }
        else if (providerType == "claude")
        {
            Add("claude-opus-4-5");
            Add("claude-sonnet-4-5");
            Add("claude-haiku-4-5");
        }

        return models;
    }

    AiProviderModel ToProviderModel(string providerType, DiscoveredModel model)
    {
        var capabilities = capabilityCatalog.Resolve(providerType, model.Id);
        return new()
        {
            Id = model.Id,
            DisplayName = model.DisplayName,
            Endpoint = model.Endpoint,
            Repository = model.Repository,
            CreatedUnix = model.CreatedUnix,
            Text = false,
            Image = false,
            Capabilities = capabilities
        };
    }

    static bool IsKnownImageModel(string providerType, string modelId) =>
        providerType switch
        {
            "openai" => modelId.Contains("image", StringComparison.OrdinalIgnoreCase) || modelId.Contains("dall-e", StringComparison.OrdinalIgnoreCase),
            "grok" => modelId.Contains("image", StringComparison.OrdinalIgnoreCase) || modelId.Contains("imagine", StringComparison.OrdinalIgnoreCase),
            "compatible" => modelId.Contains("image", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("dall-e", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    static int ModelSortRank(string providerType, string modelId)
    {
        if (providerType == "openai" && string.Equals(modelId, "gpt-image-1.5", StringComparison.Ordinal))
            return 0;
        if (IsKnownImageModel(providerType, modelId))
            return 1;
        return 2;
    }

    HttpClient CreateBearerClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken) ?? new JsonObject();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = UserFacingErrorMessageBuilder.BuildExternalHttpFailure(operation, response.StatusCode, responseBody);
        throw new ExternalServiceFailureException(message, response.StatusCode, responseBody);
    }

    static string NormalizeEndpoint(AiProvider provider)
    {
        var endpoint = string.IsNullOrWhiteSpace(provider.Endpoint) ? DefaultEndpoint(provider.Type) : provider.Endpoint.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint was empty.");

        if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint must start with http:// or https://.");

        return endpoint.EndsWith('/') ? endpoint : $"{endpoint}/";
    }

    static string DefaultEndpoint(string providerType) => providerType switch
    {
        "openai" => "https://api.openai.com/v1/",
        "grok" => "https://api.x.ai/v1/",
        "claude" => "",
        _ => ""
    };

    static bool IsOpenAiCompatible(string providerType) => providerType == "compatible";

    sealed record DiscoveredModel(
        string Id,
        string DisplayName = "",
        string Endpoint = "",
        string Repository = "",
        long? CreatedUnix = null);
}
