#pragma warning disable OPENAI001

using System.ClientModel;
using System.Security.Cryptography;
using System.Text;
using AgentRp.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;

namespace AgentRp.Services;

public interface IModelClientFactory
{
    IChatClient GetChatClient(AiProvider provider, AiProviderModel model);
    ResponsesClient GetResponsesClient(AiProvider provider, AiProviderModel model);
}

public sealed class ModelClientFactory : IModelClientFactory
{
    readonly object gate = new();
    readonly Dictionary<ClientCacheKey, ClientEntry> clients = [];

    public IChatClient GetChatClient(AiProvider provider, AiProviderModel model) =>
        GetEntry(provider, model).ChatClient;

    public ResponsesClient GetResponsesClient(AiProvider provider, AiProviderModel model) =>
        GetEntry(provider, model).ResponsesClient;

    ClientEntry GetEntry(AiProvider provider, AiProviderModel model)
    {
        var endpoint = NormalizeEndpoint(provider, model);
        var key = new ClientCacheKey(
            provider.Type.Trim().ToLowerInvariant(),
            provider.Id.Trim(),
            model.Id.Trim(),
            endpoint,
            Fingerprint(provider.ApiKey));

        lock (gate)
        {
            if (clients.TryGetValue(key, out var entry))
                return entry;

            var openAiClient = new OpenAIClient(
                new ApiKeyCredential(provider.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
            var responsesClient = openAiClient.GetResponsesClient();
            entry = new(responsesClient, responsesClient.AsIChatClient(model.Id));
            clients[key] = entry;
            return entry;
        }
    }

    public static string NormalizeEndpoint(AiProvider provider, AiProviderModel model)
    {
        var endpoint = provider.Type == "huggingface" && !string.IsNullOrWhiteSpace(model.Endpoint)
            ? model.Endpoint.Trim()
            : AiProviderEndpointRules.UsesFixedEndpoint(provider.Type)
                ? AiProviderEndpointRules.DefaultEndpoint(provider.Type)
                : string.IsNullOrWhiteSpace(provider.Endpoint) ? AiProviderEndpointRules.DefaultEndpoint(provider.Type) : provider.Endpoint.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint was empty. Responses/Open Responses providers must use a /v1-compatible base URL.");

        if (!endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Connecting to {provider.Name} failed because the endpoint must start with http:// or https://.");

        if (provider.Type == "huggingface")
            return AgentEndpointUrlNormalizer.NormalizeResponsesEndpoint(endpoint);

        return endpoint.EndsWith('/') ? endpoint : $"{endpoint}/";
    }

    static string Fingerprint(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 8));
    }

    sealed record ClientCacheKey(string ProviderType, string ProviderId, string ModelId, string Endpoint, string ApiKeyFingerprint);
    sealed record ClientEntry(ResponsesClient ResponsesClient, IChatClient ChatClient);
}
