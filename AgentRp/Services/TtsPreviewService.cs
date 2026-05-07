using AgentRp.Models;

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

public sealed class TtsPreviewService(ISpeechGenerationService speechGenerationService) : ITtsPreviewService
{
    public async Task<TtsPreviewAudio> GenerateSampleAsync(
        AiProvider provider,
        AiProviderModel model,
        AiProviderVoice voice,
        string text,
        CancellationToken cancellationToken = default)
    {
        var audio = await speechGenerationService.GenerateAsync(
            provider,
            model,
            [new(text, voice.Id)],
            cancellationToken);
        return new(audio.Bytes, audio.ContentType);
    }
}
