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

public sealed class AiProviderConnectionService(IHttpClientFactory httpClientFactory) : IAiProviderConnectionService
{
    static readonly Uri HuggingFaceWhoAmIUri = new("https://huggingface.co/api/whoami-v2");
    static readonly Uri HuggingFaceManagementBaseUri = new("https://api.endpoints.huggingface.cloud/v2/");
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
            "claude" => await DiscoverClaudeModelsAsync(provider, cancellationToken),
            "huggingface" => await DiscoverHuggingFaceModelsAsync(provider, cancellationToken),
            "compatible" => await DiscoverOpenAiCompatibleModelsAsync(provider, cancellationToken),
            _ => []
        };

        discovered = AddKnownImageModels(provider.Type, discovered);
        return discovered
            .DistinctBy(model => model.Id, StringComparer.Ordinal)
            .OrderBy(model => ModelSortRank(provider.Type, model.Id))
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .Select(model => ToProviderModel(provider.Type, model.Id))
            .ToList();
    }

    async Task<List<DiscoveredModel>> DiscoverOpenAiModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "models"), cancellationToken);
        var json = await ReadJsonAsync(response, $"Discovering OpenAI models for '{provider.Name}'", cancellationToken);
        return ReadOpenAiModelIds(json).Select(id => new DiscoveredModel(id)).ToList();
    }

    async Task<List<DiscoveredModel>> DiscoverOpenAiCompatibleModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "models"), cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
            return [];

        var json = await ReadJsonAsync(response, $"Discovering OpenAI-compatible models for '{provider.Name}'", cancellationToken);
        return ReadOpenAiModelIds(json).Select(id => new DiscoveredModel(id)).ToList();
    }

    async Task<List<DiscoveredModel>> DiscoverGrokModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "language-models"), cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken) ?? new JsonObject();
            var languageModels = json["models"]?.AsArray().SelectMany(BuildGrokModels).ToList();
            if (languageModels is { Count: > 0 })
                return languageModels;
        }

        using var fallbackResponse = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "models"), cancellationToken);
        var fallbackJson = await ReadJsonAsync(fallbackResponse, $"Discovering Grok models for '{provider.Name}'", cancellationToken);
        return ReadOpenAiModelIds(fallbackJson).Select(id => new DiscoveredModel(id)).ToList();
    }

    async Task<List<DiscoveredModel>> DiscoverClaudeModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateApiKeyClient(provider.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        using var response = await client.GetAsync(new Uri(new Uri(NormalizeEndpoint(provider)), "models"), cancellationToken);
        var json = await ReadJsonAsync(response, $"Discovering Claude models for '{provider.Name}'", cancellationToken);
        return json["data"]?.AsArray()
            .Select(node => node?["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => new DiscoveredModel(id!))
            .ToList()
            ?? [];
    }

    async Task<List<DiscoveredModel>> DiscoverHuggingFaceModelsAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var whoAmIResponse = await client.GetAsync(HuggingFaceWhoAmIUri, cancellationToken);
        var whoAmI = await ReadJsonAsync(whoAmIResponse, $"Discovering Hugging Face namespaces for '{provider.Name}'", cancellationToken);
        var namespaces = new List<string>();
        AddNamespace(namespaces, whoAmI["name"]?.GetValue<string>());
        foreach (var org in whoAmI["orgs"]?.AsArray() ?? [])
            AddNamespace(namespaces, org?["name"]?.GetValue<string>());

        var models = new List<DiscoveredModel>();
        foreach (var @namespace in namespaces)
        {
            var requestUri = new Uri(HuggingFaceManagementBaseUri, $"endpoint/{Uri.EscapeDataString(@namespace)}?limit=100");
            using var response = await client.GetAsync(requestUri, cancellationToken);
            var json = await ReadJsonAsync(response, $"Discovering Hugging Face endpoints for '{provider.Name}'", cancellationToken);
            foreach (var item in json["items"]?.AsArray() ?? [])
            {
                var endpointName = item?["name"]?.GetValue<string>();
                var repository = item?["model"]?["repository"]?.GetValue<string>();
                var modelId = string.IsNullOrWhiteSpace(repository) ? endpointName : repository;
                if (!string.IsNullOrWhiteSpace(modelId))
                    models.Add(new(modelId));
            }
        }

        return models;
    }

    static IEnumerable<DiscoveredModel> BuildGrokModels(JsonNode? node)
    {
        var id = node?["id"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(id))
            yield return new(id);

        foreach (var alias in node?["aliases"]?.AsArray() ?? [])
        {
            var aliasId = alias?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(aliasId))
                yield return new(aliasId);
        }
    }

    static IEnumerable<string> ReadOpenAiModelIds(JsonNode json) =>
        json["data"]?.AsArray()
            .Select(node => node?["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
        ?? [];

    static List<DiscoveredModel> AddKnownImageModels(string providerType, List<DiscoveredModel> models)
    {
        void Add(string id)
        {
            if (models.All(model => !string.Equals(model.Id, id, StringComparison.Ordinal)))
                models.Add(new(id));
        }

        if (providerType == "openai")
        {
            Add("gpt-image-1.5");
            Add("gpt-image-1");
            Add("gpt-image-1-mini");
        }
        else if (providerType == "grok")
        {
            Add("grok-imagine-image");
        }

        return models;
    }

    static AiProviderModel ToProviderModel(string providerType, string modelId)
    {
        var image = IsKnownImageModel(providerType, modelId);
        return new()
        {
            Id = modelId,
            Text = !image,
            Image = image
        };
    }

    static bool IsKnownImageModel(string providerType, string modelId) =>
        providerType switch
        {
            "openai" => modelId.Contains("image", StringComparison.OrdinalIgnoreCase) || modelId.Contains("dall-e", StringComparison.OrdinalIgnoreCase),
            "grok" => modelId.Contains("image", StringComparison.OrdinalIgnoreCase) || modelId.Contains("imagine", StringComparison.OrdinalIgnoreCase),
            "huggingface" => modelId.Contains("flux", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("stable-diffusion", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("sdxl", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("image", StringComparison.OrdinalIgnoreCase),
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

    HttpClient CreateApiKeyClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);

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
        "claude" => "https://api.anthropic.com/v1/",
        "huggingface" => "https://api.endpoints.huggingface.cloud/v2/",
        _ => ""
    };

    static bool IsOpenAiCompatible(string providerType) => providerType == "compatible";

    static void AddNamespace(List<string> namespaces, string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && namespaces.All(value => !string.Equals(value, candidate, StringComparison.Ordinal)))
            namespaces.Add(candidate);
    }

    sealed record DiscoveredModel(string Id);
}
