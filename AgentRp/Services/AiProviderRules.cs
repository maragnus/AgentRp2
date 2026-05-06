namespace AgentRp.Services;

public static class AiProviderEndpointRules
{
    public static bool UsesFixedEndpoint(string providerType) => providerType.Trim().ToLowerInvariant() is "openai" or "grok" or "claude";

    public static string DefaultEndpoint(string providerType) => providerType.Trim().ToLowerInvariant() switch
    {
        "openai" => "https://api.openai.com/v1/",
        "grok" => "https://api.x.ai/v1/",
        "claude" => "https://api.anthropic.com/v1/",
        _ => ""
    };
}

public static class AiProviderModelIdentityRules
{
    public static bool IsKnownImageGenerationModel(string providerType, string modelId) =>
        providerType.Trim().ToLowerInvariant() switch
        {
            "openai" => IsOpenAiImageModel(modelId),
            "grok" => modelId.Contains("image", StringComparison.OrdinalIgnoreCase)
                || modelId.Contains("imagine", StringComparison.OrdinalIgnoreCase),
            "compatible" => IsOpenAiImageModel(modelId),
            _ => false
        };

    static bool IsOpenAiImageModel(string modelId) =>
        modelId.Contains("image", StringComparison.OrdinalIgnoreCase)
        || modelId.Contains("dall-e", StringComparison.OrdinalIgnoreCase);
}
