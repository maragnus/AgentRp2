using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Serialization;

namespace AgentRp.Services;

public interface IAiProviderVoiceDiscoveryService
{
    Task<IReadOnlyList<AiProviderVoice>> RefreshVoicesAsync(AiProvider provider, AiProviderModel model, CancellationToken cancellationToken = default);
}

public sealed class AiProviderVoiceDiscoveryService(IHttpClientFactory httpClientFactory) : IAiProviderVoiceDiscoveryService
{
    static readonly IReadOnlyDictionary<string, IReadOnlyList<AiProviderVoice>> OpenAiVoicesByModel =
        new Dictionary<string, IReadOnlyList<AiProviderVoice>>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o-mini-tts"] = BuildOpenAiVoices(
                ["alloy", "ash", "ballad", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer", "verse", "marin", "cedar"]),
            ["tts-1"] = BuildOpenAiVoices(
                ["alloy", "ash", "coral", "echo", "fable", "onyx", "nova", "sage", "shimmer"]),
            ["tts-1-hd"] = BuildOpenAiVoices(
                ["alloy", "ash", "coral", "echo", "fable", "onyx", "nova", "sage", "shimmer"])
        };

    public async Task<IReadOnlyList<AiProviderVoice>> RefreshVoicesAsync(
        AiProvider provider,
        AiProviderModel model,
        CancellationToken cancellationToken = default)
    {
        var voices = provider.Type switch
        {
            "openai" => OpenAiVoicesByModel.GetValueOrDefault(model.Id) ?? [],
            "grok" => await DiscoverXAiVoicesAsync(provider, cancellationToken),
            "elevenlabs" => await DiscoverElevenLabsVoicesAsync(provider, model, cancellationToken),
            _ => []
        };

        return voices
            .DistinctBy(voice => voice.Id, StringComparer.Ordinal)
            .OrderBy(voice => voice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(voice => voice.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    async Task<IReadOnlyList<AiProviderVoice>> DiscoverXAiVoicesAsync(AiProvider provider, CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        var voices = new List<AiProviderVoice>();
        using var response = await client.GetAsync(new Uri("https://api.x.ai/v1/tts/voices"), cancellationToken);
        var json = await ReadJsonAsync(response, $"Refreshing xAI voices for '{provider.Name}'", cancellationToken);
        voices.AddRange(ReadXAiVoices(json, "xai"));

        using var customResponse = await client.GetAsync(new Uri("https://api.x.ai/v1/custom-voices"), cancellationToken);
        if (customResponse.IsSuccessStatusCode)
        {
            var customJson = await customResponse.Content.ReadFromJsonAsync<JsonNode>(AppJsonSerializerOptions.Web, cancellationToken) ?? new JsonObject();
            voices.AddRange(ReadXAiVoices(customJson, "xai-custom"));
        }
        else if (customResponse.StatusCode is not HttpStatusCode.NotFound and not HttpStatusCode.MethodNotAllowed)
        {
            var responseBody = await customResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new ExternalServiceFailureException(
                UserFacingErrorMessageBuilder.BuildExternalHttpFailure("Refreshing xAI custom voices", customResponse.StatusCode, responseBody, "xAI"),
                customResponse.StatusCode,
                responseBody);
        }

        return voices;
    }

    async Task<IReadOnlyList<AiProviderVoice>> DiscoverElevenLabsVoicesAsync(
        AiProvider provider,
        AiProviderModel model,
        CancellationToken cancellationToken)
    {
        using var client = CreateElevenLabsClient(provider.ApiKey);
        var voices = new List<AiProviderVoice>();
        string? token = null;
        do
        {
            var path = "voices?page_size=100";
            if (!string.IsNullOrWhiteSpace(token))
                path += $"&next_page_token={Uri.EscapeDataString(token)}";

            using var response = await client.GetAsync(new Uri(new Uri("https://api.elevenlabs.io/v2/"), path), cancellationToken);
            var json = await ReadJsonAsync(response, $"Refreshing ElevenLabs voices for '{provider.Name}'", cancellationToken);
            voices.AddRange(ReadElevenLabsVoices(json, model.Id));
            token = json["has_more"]?.GetValue<bool>() == true
                ? json["next_page_token"]?.GetValue<string>()
                : null;
        }
        while (!string.IsNullOrWhiteSpace(token));

        return voices;
    }

    static IEnumerable<AiProviderVoice> ReadXAiVoices(JsonNode json, string source)
    {
        var nodes = json["voices"]?.AsArray() ?? json["data"]?.AsArray() ?? [];
        foreach (var node in nodes)
        {
            var id = node?["voice_id"]?.GetValue<string>() ?? node?["id"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id))
                continue;

            yield return new()
            {
                Id = id,
                DisplayName = node?["name"]?.GetValue<string>() ?? id,
                Description = node?["description"]?.GetValue<string>() ?? node?["tone"]?.GetValue<string>() ?? "",
                Source = source,
                Labels = ReadLabels(node),
                UpdatedUtc = DateTime.UtcNow
            };
        }
    }

    static IEnumerable<AiProviderVoice> ReadElevenLabsVoices(JsonNode json, string modelId)
    {
        foreach (var node in json["voices"]?.AsArray() ?? [])
        {
            var id = node?["voice_id"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id) || !SupportsElevenLabsModel(node, modelId))
                continue;

            yield return new()
            {
                Id = id,
                DisplayName = node?["name"]?.GetValue<string>() ?? id,
                Description = node?["description"]?.GetValue<string>() ?? "",
                PreviewUrl = node?["preview_url"]?.GetValue<string>() ?? "",
                Source = "elevenlabs",
                Labels = ReadLabels(node),
                UpdatedUtc = DateTime.UtcNow
            };
        }
    }

    static bool SupportsElevenLabsModel(JsonNode? node, string modelId)
    {
        var supported = node?["high_quality_base_model_ids"]?.AsArray()
            .Select(value => value?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];

        return supported.Count == 0 || supported.Contains(modelId);
    }

    static Dictionary<string, string> ReadLabels(JsonNode? node)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var labelObject = node?["labels"]?.AsObject();
        if (labelObject is null)
            return labels;

        foreach (var pair in labelObject)
        {
            var value = pair.Value?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
                labels[pair.Key] = value;
        }

        return labels;
    }

    static IReadOnlyList<AiProviderVoice> BuildOpenAiVoices(IReadOnlyList<string> ids) =>
        ids.Select(id => new AiProviderVoice
        {
            Id = id,
            DisplayName = DisplayVoiceName(id),
            Source = "openai-catalog",
            UpdatedUtc = DateTime.UtcNow
        }).ToList();

    static string DisplayVoiceName(string id) =>
        string.IsNullOrWhiteSpace(id) ? id : char.ToUpperInvariant(id[0]) + id[1..];

    HttpClient CreateBearerClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    HttpClient CreateElevenLabsClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        return client;
    }

    static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<JsonNode>(AppJsonSerializerOptions.Web, cancellationToken) ?? new JsonObject();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new ExternalServiceFailureException(
            UserFacingErrorMessageBuilder.BuildExternalHttpFailure(operation, response.StatusCode, responseBody),
            response.StatusCode,
            responseBody);
    }
}
