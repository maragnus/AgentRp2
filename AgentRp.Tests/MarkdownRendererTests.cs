using AgentRp.Services;

namespace AgentRp.Tests;

public sealed class MarkdownRendererTests
{
    readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void RenderConvertsEmphasis()
    {
        var html = _renderer.Render("A *warm* line.");

        Assert.Contains("<em>warm</em>", html);
    }

    [Fact]
    public void RenderSplitsParagraphsOnBlankLines()
    {
        var html = _renderer.Render("First paragraph.\n\nSecond paragraph.");

        Assert.Contains("<p>First paragraph.</p>", html);
        Assert.Contains("<p>Second paragraph.</p>", html);
    }

    [Fact]
    public void RenderTreatsSoftLineBreaksAsHardBreaks()
    {
        var html = _renderer.Render("First line.\nSecond line.");

        Assert.Contains("First line.", html);
        Assert.Contains("Second line.", html);
        Assert.Contains("<br", html);
    }

    [Fact]
    public void RenderPreservesRawHtml()
    {
        var html = _renderer.Render("Before <span>here</span> after.");

        Assert.Contains("<span>here</span>", html);
    }
}
