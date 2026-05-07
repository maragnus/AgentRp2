using Markdig;

namespace AgentRp.Services;

public interface IMarkdownRenderer
{
    string Render(string markdown);
}

public sealed class MarkdownRenderer : IMarkdownRenderer
{
    readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAudioTags()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public string Render(string markdown) => Markdown.ToHtml(markdown?.Trim() ?? string.Empty, _pipeline);
}
