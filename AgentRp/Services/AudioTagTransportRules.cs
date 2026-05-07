using System.Text;
using AgentRp.Models;

namespace AgentRp.Services;

public static class AudioTagTransportRules
{
    public static bool SupportsAudioTags(ActiveModelSelection selection) =>
        SupportsAudioTags(selection.Provider, selection.Model);

    public static bool SupportsAudioTags(AiProvider provider, AiProviderModel model) =>
        provider.Type.Trim().ToLowerInvariant() switch
        {
            "grok" or "xai" => true,
            "elevenlabs" => string.Equals(model.Id.Trim(), "eleven_v3", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    public static string StripAudioTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '[' && TrySkipSquareTag(text, index, out var squareEnd))
            {
                index = squareEnd;
                continue;
            }

            if (current == '<' && TrySkipXmlTag(text, index, out var xmlEnd))
            {
                index = xmlEnd;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    internal static bool TryReadSquareTag(string text, int start, out int end) =>
        TrySkipSquareTag(text, start, out end);

    static bool TrySkipSquareTag(string text, int start, out int end)
    {
        end = start;
        for (var index = start + 1; index < text.Length; index++)
        {
            var current = text[index];
            if (current is '\r' or '\n' or '[')
                return false;

            if (current != ']')
                continue;

            if (index == start + 1)
                return false;

            var afterClose = index + 1;
            if (afterClose < text.Length && text[afterClose] == '(')
                return false;

            end = index;
            return true;
        }

        return false;
    }

    static bool TrySkipXmlTag(string text, int start, out int end)
    {
        end = start;
        var index = start + 1;
        if (index >= text.Length)
            return false;

        if (text[index] == '/')
            index++;

        if (index >= text.Length || !IsTagNameStart(text[index]))
            return false;

        index++;
        while (index < text.Length && IsTagNamePart(text[index]))
            index++;

        if (index >= text.Length || text[index] != '>')
            return false;

        end = index;
        return true;
    }

    static bool IsTagNameStart(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    static bool IsTagNamePart(char value) =>
        IsTagNameStart(value) || value is >= '0' and <= '9' or '-';
}
