using AgentRp.Models;

namespace AgentRp.Components.Common;

public static class AiProviderVoiceDisplayRules
{
    public static string DisplayVoice(AiProviderVoice voice) =>
        string.IsNullOrWhiteSpace(voice.DisplayName) ? voice.Id : voice.DisplayName;

    public static IReadOnlyList<string> VisibleTags(AiProvider provider, AiProviderVoice voice) =>
        voice.Labels
            .Where(pair => IsVisibleTag(provider, pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    static bool IsVisibleTag(AiProvider provider, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var tag = value.Trim();
        return !IsProviderTag(provider, tag)
            && !IsProviderTag(provider, tag.Replace(" ", "", StringComparison.Ordinal))
            && !IsProviderTag(provider, tag.Replace("-", "", StringComparison.Ordinal));
    }

    static bool IsProviderTag(AiProvider provider, string tag)
    {
        var providerType = provider.Type.Trim();
        var providerName = provider.Name.Trim();
        return string.Equals(tag, providerType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, providerName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "xai", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "grok", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "elevenlabs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tag, "openai", StringComparison.OrdinalIgnoreCase);
    }
}
