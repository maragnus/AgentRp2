using System.Net;
using System.Text.RegularExpressions;

namespace AgentRp.Services;

public interface ITranscriptBodyRenderer
{
    string Render(string markdown);
}

public sealed partial class TranscriptBodyRenderer(IMarkdownRenderer markdownRenderer) : ITranscriptBodyRenderer
{
    public string Render(string markdown) =>
        markdownRenderer.Render(MarkAudioTags(markdown ?? string.Empty));

    public static string MarkAudioTags(string markdown)
    {
        var withSpeechTags = SpeechTagRegex().Replace(markdown, match => AudioTagSpan(match.Value));
        return SquareAudioTagRegex().Replace(withSpeechTags, match => AudioTagSpan(match.Value));
    }

    static string AudioTagSpan(string tag) =>
        $"""<span class="audio-tag">{WebUtility.HtmlEncode(tag)}</span>""";

    [GeneratedRegex(@"</?(?:whisper|soft|loud|slow|fast|build-intensity|decrease-intensity|higher-pitch|lower-pitch|sing-song|singing|laugh-speak|emphasis)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpeechTagRegex();

    [GeneratedRegex(@"\[(?<tag>[A-Za-z][A-Za-z0-9 .'-]{0,40})\](?!\()", RegexOptions.CultureInvariant)]
    private static partial Regex SquareAudioTagRegex();
}
