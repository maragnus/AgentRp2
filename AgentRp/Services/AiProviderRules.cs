using AgentRp.Models;

namespace AgentRp.Services;

public static class AiProviderEndpointRules
{
    public static bool UsesFixedEndpoint(string providerType) => providerType.Trim().ToLowerInvariant() is "openai" or "grok" or "claude" or "elevenlabs";

    public static string DefaultEndpoint(string providerType) => providerType.Trim().ToLowerInvariant() switch
    {
        "openai" => "https://api.openai.com/v1/",
        "grok" => "https://api.x.ai/v1/",
        "claude" => "https://api.anthropic.com/v1/",
        "elevenlabs" => "https://api.elevenlabs.io/v1/",
        _ => ""
    };
}

public static class AiProviderModelIdentityRules
{
    public const string XAiTextToSpeechModelId = "xai-tts";
    const string XAiTextToSpeechDisplayName = "xAI Text to Speech";

    public static bool IsProviderManagedVoiceEndpoint(string providerType, string modelId) =>
        providerType.Trim().ToLowerInvariant() == "grok"
        && string.Equals(modelId, XAiTextToSpeechModelId, StringComparison.OrdinalIgnoreCase);

    public static bool EnsureProviderManagedVoiceModels(AiProvider provider, IModelCapabilityCatalog capabilityCatalog)
    {
        if (provider.Type.Trim().ToLowerInvariant() != "grok")
            return false;

        var changed = false;
        var model = provider.Models.FirstOrDefault(model => IsProviderManagedVoiceEndpoint(provider.Type, model.Id));
        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            if (model is null)
                return false;

            if (model.Enabled || model.Roles.Count > 0)
            {
                AiProviderModelSelectionRules.ClearSelectedRoles(model);
                changed = true;
            }

            return changed;
        }

        if (model is null)
        {
            model = new()
            {
                Id = XAiTextToSpeechModelId,
                DisplayName = XAiTextToSpeechDisplayName
            };
            provider.Models.Add(model);
            changed = true;
        }

        if (!string.Equals(model.DisplayName, XAiTextToSpeechDisplayName, StringComparison.Ordinal))
        {
            model.DisplayName = XAiTextToSpeechDisplayName;
            changed = true;
        }

        model.Capabilities = capabilityCatalog.Resolve(provider.Type, model.Id);
        if (!AiProviderModelSelectionRules.IsSelectedForVoice(model))
        {
            AiProviderModelSelectionRules.SetVoiceSelected(model, true);
            changed = true;
        }

        return changed;
    }

    public static bool IsKnownImageGenerationModel(string providerType, string modelId) =>
        providerType.Trim().ToLowerInvariant() switch
        {
            "openai" => IsOpenAiImageModel(modelId),
            "grok" => modelId.Contains("image", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("imagine", StringComparison.OrdinalIgnoreCase),
            "compatible" => IsOpenAiImageModel(modelId),
            _ => false
        };

    public static bool IsKnownSpeechModel(string providerType, string modelId) =>
        providerType.Trim().ToLowerInvariant() switch
        {
            "openai" => IsOpenAiSpeechModel(modelId),
            "grok" => IsXAiSpeechModel(modelId),
            "elevenlabs" => IsElevenLabsSpeechModel(modelId),
            _ => modelId.Contains("tts", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("speech", StringComparison.OrdinalIgnoreCase)
        };

    public static bool IsKnownSpeechToTextModel(string providerType, string modelId) =>
        providerType.Trim().ToLowerInvariant() switch
        {
            "openai" => modelId.Contains("whisper", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("transcribe", StringComparison.OrdinalIgnoreCase),
            "grok" => modelId.Contains("stt", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("transcribe", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    static bool IsOpenAiImageModel(string modelId) =>
        modelId.Contains("image", StringComparison.OrdinalIgnoreCase)
        || modelId.Contains("dall-e", StringComparison.OrdinalIgnoreCase);

    static bool IsOpenAiSpeechModel(string modelId) =>
        modelId.Contains("tts", StringComparison.OrdinalIgnoreCase)
        || modelId.Equals("tts-1", StringComparison.OrdinalIgnoreCase)
        || modelId.Equals("tts-1-hd", StringComparison.OrdinalIgnoreCase);

    static bool IsXAiSpeechModel(string modelId) =>
        modelId.Equals(XAiTextToSpeechModelId, StringComparison.OrdinalIgnoreCase)
        || modelId.Contains("tts", StringComparison.OrdinalIgnoreCase)
        || modelId.Contains("speech", StringComparison.OrdinalIgnoreCase);

    static bool IsElevenLabsSpeechModel(string modelId) =>
        modelId.StartsWith("eleven_", StringComparison.OrdinalIgnoreCase)
        && !modelId.Contains("scribe", StringComparison.OrdinalIgnoreCase);
}
