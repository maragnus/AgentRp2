using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgentRp.Models;
using AgentRp.Serialization;

namespace AgentRp.Services;

public interface ITtsPreviewService
{
    Task<TtsPreviewAudio> GenerateSampleAsync(
        AiProvider provider,
        AiProviderModel model,
        AiProviderVoice voice,
        string text,
        CancellationToken cancellationToken = default);
}

public sealed record TtsPreviewAudio(byte[] Bytes, string ContentType);

public sealed class TtsPreviewService(IHttpClientFactory httpClientFactory) : ITtsPreviewService
{
    public Task<TtsPreviewAudio> GenerateSampleAsync(
        AiProvider provider,
        AiProviderModel model,
        AiProviderVoice voice,
        string text,
        CancellationToken cancellationToken = default) =>
        provider.Type switch
        {
            "openai" => GenerateOpenAiSampleAsync(provider, model, voice, text, cancellationToken),
            "grok" => GenerateXAiSampleAsync(provider, voice, text, cancellationToken),
            "elevenlabs" => GenerateElevenLabsSampleAsync(provider, model, voice, text, cancellationToken),
            _ => throw new InvalidOperationException($"Generating a voice sample failed because {provider.Name} does not support text-to-speech previews.")
        };

    async Task<TtsPreviewAudio> GenerateOpenAiSampleAsync(
        AiProvider provider,
        AiProviderModel model,
        AiProviderVoice voice,
        string text,
        CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.PostAsJsonAsync(
            new Uri(new Uri(AiProviderEndpointRules.DefaultEndpoint(provider.Type)), "audio/speech"),
            new
            {
                model = model.Id,
                voice = voice.Id,
                input = text,
                response_format = "mp3"
            },
            AppJsonSerializerOptions.Web,
            cancellationToken);

        return await ReadAudioAsync(response, $"Generating OpenAI voice sample for {DisplayVoice(voice)}", "OpenAI", cancellationToken);
    }

    async Task<TtsPreviewAudio> GenerateXAiSampleAsync(
        AiProvider provider,
        AiProviderVoice voice,
        string text,
        CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.PostAsJsonAsync(
            new Uri("https://api.x.ai/v1/tts"),
            new
            {
                text,
                voice_id = voice.Id,
                language = "en"
            },
            AppJsonSerializerOptions.Web,
            cancellationToken);

        return await ReadAudioAsync(response, $"Generating xAI voice sample for {DisplayVoice(voice)}", "xAI", cancellationToken);
    }

    async Task<TtsPreviewAudio> GenerateElevenLabsSampleAsync(
        AiProvider provider,
        AiProviderModel model,
        AiProviderVoice voice,
        string text,
        CancellationToken cancellationToken)
    {
        using var client = CreateElevenLabsClient(provider.ApiKey);
        using var response = await client.PostAsJsonAsync(
            new Uri($"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(voice.Id)}?output_format=mp3_44100_128"),
            new
            {
                text,
                model_id = model.Id
            },
            AppJsonSerializerOptions.Web,
            cancellationToken);

        return await ReadAudioAsync(response, $"Generating ElevenLabs voice sample for {DisplayVoice(voice)}", "ElevenLabs", cancellationToken);
    }

    HttpClient CreateBearerClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(45);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    HttpClient CreateElevenLabsClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(45);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        return client;
    }

    static async Task<TtsPreviewAudio> ReadAudioAsync(
        HttpResponseMessage response,
        string operation,
        string serviceName,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ExternalServiceFailureException(
                UserFacingErrorMessageBuilder.BuildExternalHttpFailure(operation, response.StatusCode, responseBody, serviceName),
                response.StatusCode,
                responseBody);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "audio/mpeg";

        return new(await response.Content.ReadAsByteArrayAsync(cancellationToken), contentType);
    }

    static string DisplayVoice(AiProviderVoice voice) =>
        string.IsNullOrWhiteSpace(voice.DisplayName) ? voice.Id : voice.DisplayName;
}
