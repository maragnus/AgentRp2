using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentRp.Models;
using AgentRp.Serialization;

namespace AgentRp.Services;

public sealed record SpeechGenerationInput(string Text, string VoiceId);

public static class SpeechGenerationInputJson
{
    public static IReadOnlyList<SpeechGenerationInput> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SpeechGenerationInput>>(json, AppJsonSerializerOptions.Web) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed record SpeechAudio(byte[] Bytes, string ContentType);

public sealed record SpeechAudioChunk(byte[] Bytes, string ContentType);

public interface ISpeechGenerationService
{
    IAsyncEnumerable<SpeechAudioChunk> StreamAsync(
        AiProvider provider,
        AiProviderModel model,
        IReadOnlyList<SpeechGenerationInput> inputs,
        CancellationToken cancellationToken = default);

    Task<SpeechAudio> GenerateAsync(
        AiProvider provider,
        AiProviderModel model,
        IReadOnlyList<SpeechGenerationInput> inputs,
        CancellationToken cancellationToken = default);
}

public sealed class SpeechGenerationService(IHttpClientFactory httpClientFactory) : ISpeechGenerationService
{
    public async Task<SpeechAudio> GenerateAsync(
        AiProvider provider,
        AiProviderModel model,
        IReadOnlyList<SpeechGenerationInput> inputs,
        CancellationToken cancellationToken = default)
    {
        await using var audio = new MemoryStream();
        var contentType = "audio/mpeg";
        await foreach (var chunk in StreamAsync(provider, model, inputs, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(chunk.ContentType))
                contentType = chunk.ContentType;

            await audio.WriteAsync(chunk.Bytes, cancellationToken);
        }

        return new(audio.ToArray(), contentType);
    }

    public IAsyncEnumerable<SpeechAudioChunk> StreamAsync(
        AiProvider provider,
        AiProviderModel model,
        IReadOnlyList<SpeechGenerationInput> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs.Count == 0)
            throw new InvalidOperationException("Generating speech failed because there was no text to read.");

        return provider.Type.Trim().ToLowerInvariant() switch
        {
            "openai" => StreamOpenAiAsync(provider, model, SingleInput(inputs), cancellationToken),
            "grok" or "xai" => StreamXAiAsync(provider, SingleInput(inputs), cancellationToken),
            "elevenlabs" when inputs.Count == 1 => StreamElevenLabsSpeechAsync(provider, model, inputs[0], cancellationToken),
            "elevenlabs" => StreamElevenLabsDialogueAsync(provider, model, inputs, cancellationToken),
            _ => throw new InvalidOperationException($"Generating speech failed because {provider.Name} does not support text-to-speech.")
        };
    }

    async IAsyncEnumerable<SpeechAudioChunk> StreamOpenAiAsync(
        AiProvider provider,
        AiProviderModel model,
        SpeechGenerationInput input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.PostAsJsonAsync(
            new Uri(new Uri(AiProviderEndpointRules.DefaultEndpoint(provider.Type)), "audio/speech"),
            new
            {
                model = model.Id,
                voice = input.VoiceId,
                input = input.Text,
                response_format = "mp3"
            },
            AppJsonSerializerOptions.Web,
            cancellationToken);

        yield return await ReadAudioAsync(response, $"Generating speech with {provider.Name}", provider.Name, cancellationToken);
    }

    async IAsyncEnumerable<SpeechAudioChunk> StreamXAiAsync(
        AiProvider provider,
        SpeechGenerationInput input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = CreateBearerClient(provider.ApiKey);
        using var response = await client.PostAsJsonAsync(
            new Uri("https://api.x.ai/v1/tts"),
            new
            {
                text = input.Text,
                voice_id = input.VoiceId,
                language = "en"
            },
            AppJsonSerializerOptions.Web,
            cancellationToken);

        yield return await ReadAudioAsync(response, $"Generating speech with {provider.Name}", provider.Name, cancellationToken);
    }

    async IAsyncEnumerable<SpeechAudioChunk> StreamElevenLabsSpeechAsync(
        AiProvider provider,
        AiProviderModel model,
        SpeechGenerationInput input,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = CreateElevenLabsClient(provider.ApiKey);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(input.VoiceId)}/stream?output_format=mp3_44100_128"))
        {
            Content = JsonContent.Create(
                new
                {
                    text = input.Text,
                    model_id = model.Id
                },
                options: AppJsonSerializerOptions.Web)
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await foreach (var chunk in ReadAudioChunksAsync(response, $"Generating speech with {provider.Name}", provider.Name, cancellationToken))
            yield return chunk;
    }

    async IAsyncEnumerable<SpeechAudioChunk> StreamElevenLabsDialogueAsync(
        AiProvider provider,
        AiProviderModel model,
        IReadOnlyList<SpeechGenerationInput> inputs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = CreateElevenLabsClient(provider.ApiKey);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("https://api.elevenlabs.io/v1/text-to-dialogue/stream?output_format=mp3_44100_128"))
        {
            Content = JsonContent.Create(
                new
                {
                    inputs = inputs.Select(input => new
                    {
                        text = input.Text,
                        voice_id = input.VoiceId
                    }).ToList(),
                    model_id = model.Id
                },
                options: AppJsonSerializerOptions.Web)
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await foreach (var chunk in ReadAudioChunksAsync(response, $"Generating dialogue with {provider.Name}", provider.Name, cancellationToken))
            yield return chunk;
    }

    async Task<SpeechAudioChunk> ReadAudioAsync(
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

    static async IAsyncEnumerable<SpeechAudioChunk> ReadAudioChunksAsync(
        HttpResponseMessage response,
        string operation,
        string serviceName,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            var bytes = new byte[read];
            Buffer.BlockCopy(buffer, 0, bytes, 0, read);
            yield return new(bytes, contentType);
        }
    }

    HttpClient CreateBearerClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return client;
    }

    HttpClient CreateElevenLabsClient(string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        return client;
    }

    static SpeechGenerationInput SingleInput(IReadOnlyList<SpeechGenerationInput> inputs)
    {
        if (inputs.Count == 1)
            return inputs[0];

        var first = inputs[0];
        return first with { Text = string.Join(" ", inputs.Select(input => input.Text).Where(text => !string.IsNullOrWhiteSpace(text))) };
    }
}
