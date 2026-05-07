using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AgentRp.Services;

internal static class MarkdownAudioTagExtensions
{
    public static MarkdownPipelineBuilder UseAudioTags(this MarkdownPipelineBuilder builder)
    {
        builder.Extensions.AddIfNotAlready<AudioTagMarkdownExtension>();
        return builder;
    }
}

internal sealed class AudioTagMarkdownExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (pipeline.InlineParsers.Contains<AudioTagInlineParser>())
            return;

        pipeline.InlineParsers.Insert(0, new AudioTagInlineParser());
    }

    public void Setup(MarkdownPipeline pipeline, Markdig.Renderers.IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
            htmlRenderer.ObjectRenderers.AddIfNotAlready(new HtmlAudioTagRenderer());
    }
}

internal sealed class AudioTagInline(string text) : LeafInline
{
    public string Text { get; } = text;
}

internal sealed class AudioTagInlineParser : InlineParser
{
    public AudioTagInlineParser() => OpeningCharacters = ['[', '<'];

    public override bool Match(InlineProcessor processor, ref StringSlice slice) =>
        slice.CurrentChar switch
        {
            '[' => TryMatchSquareTag(processor, ref slice),
            '<' => TryMatchXmlTag(processor, ref slice),
            _ => false
        };

    static bool TryMatchSquareTag(InlineProcessor processor, ref StringSlice slice)
    {
        var text = slice.Text;
        var start = slice.Start;

        for (var index = start + 1; index <= slice.End; index++)
        {
            var current = text[index];
            if (current is '\r' or '\n' or '[')
                return false;

            if (current != ']')
                continue;

            if (index == start + 1)
                return false;

            var afterClose = index + 1;
            if (afterClose <= slice.End && text[afterClose] == '(')
                return false;

            Emit(processor, ref slice, start, index);
            return true;
        }

        return false;
    }

    static bool TryMatchXmlTag(InlineProcessor processor, ref StringSlice slice)
    {
        var text = slice.Text;
        var start = slice.Start;
        var index = start + 1;

        if (index > slice.End)
            return false;

        if (text[index] == '/')
            index++;

        if (index > slice.End || !IsTagNameStart(text[index]))
            return false;

        index++;
        while (index <= slice.End && IsTagNamePart(text[index]))
            index++;

        if (index > slice.End || text[index] != '>')
            return false;

        Emit(processor, ref slice, start, index);
        return true;
    }

    static void Emit(InlineProcessor processor, ref StringSlice slice, int start, int end)
    {
        var tag = slice.Text.Substring(start, end - start + 1);
        var audioTag = new AudioTagInline(tag)
        {
            Span = { Start = processor.GetSourcePosition(start, out var line, out var column) },
            Line = line,
            Column = column
        };
        audioTag.Span.End = audioTag.Span.Start + tag.Length - 1;
        processor.Inline = audioTag;
        slice.Start = end + 1;
    }

    static bool IsTagNameStart(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    static bool IsTagNamePart(char value) =>
        IsTagNameStart(value) || value is >= '0' and <= '9' or '-';
}

internal sealed class HtmlAudioTagRenderer : HtmlObjectRenderer<AudioTagInline>
{
    protected override void Write(HtmlRenderer renderer, AudioTagInline obj) =>
        renderer
            .Write("<span class=\"audio-tag\">")
            .WriteEscape(obj.Text)
            .Write("</span>");
}
