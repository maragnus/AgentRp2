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
    public void RenderMarksSimpleHtmlShapedTagsAsAudioTags()
    {
        var html = _renderer.Render("Before <span>here</span> after.");

        Assert.Contains("Before ", html);
        Assert.Contains("""<span class="audio-tag">&lt;span&gt;</span>""", html);
        Assert.Contains("here", html);
        Assert.Contains("""<span class="audio-tag">&lt;/span&gt;</span>""", html);
    }

    [Fact]
    public void RenderMarksSquareAudioTags()
    {
        var html = _renderer.Render("[whispers] \"Stay close.\"");

        Assert.Contains("""<span class="audio-tag">[whispers]</span>""", html);
        Assert.Contains("Stay close.", html);
    }

    [Fact]
    public void RenderMarksArbitrarySquareAudioTags()
    {
        var html = _renderer.Render("[door creaks] [long-pause] [whatever comes next]");

        Assert.Contains("""<span class="audio-tag">[door creaks]</span>""", html);
        Assert.Contains("""<span class="audio-tag">[long-pause]</span>""", html);
        Assert.Contains("""<span class="audio-tag">[whatever comes next]</span>""", html);
    }

    [Fact]
    public void RenderMarksXmlLikeAudioTagsAsVisibleText()
    {
        var html = _renderer.Render("<whisper>Stay close.</whisper>");

        Assert.Contains("""<span class="audio-tag">&lt;whisper&gt;</span>""", html);
        Assert.Contains("Stay close.", html);
        Assert.Contains("""<span class="audio-tag">&lt;/whisper&gt;</span>""", html);
    }

    [Fact]
    public void RenderMarksExtendedXmlLikeAudioTagsAsVisibleText()
    {
        var html = _renderer.Render("<build-intensity>Stay close.</build-intensity>");

        Assert.Contains("""<span class="audio-tag">&lt;build-intensity&gt;</span>""", html);
        Assert.Contains("Stay close.", html);
        Assert.Contains("""<span class="audio-tag">&lt;/build-intensity&gt;</span>""", html);
    }

    [Fact]
    public void RenderDoesNotConsumeMarkdownLinks()
    {
        var html = _renderer.Render("[docs](https://example.test)");

        Assert.Contains("""<a href="https://example.test">docs</a>""", html);
        Assert.DoesNotContain("audio-tag", html);
    }

    [Fact]
    public void RenderDoesNotConsumeMarkdownImages()
    {
        var html = _renderer.Render("![alt](image.png)");

        Assert.Contains("<img src=\"image.png\" alt=\"alt\"", html);
        Assert.DoesNotContain("audio-tag", html);
    }

    [Fact]
    public void RenderDoesNotConsumeAudioTagsInsideCodeSpans()
    {
        var html = _renderer.Render("`[pause] <whisper>`");

        Assert.Contains("<code>[pause] &lt;whisper&gt;</code>", html);
        Assert.DoesNotContain("audio-tag", html);
    }

    [Fact]
    public void RenderEscapesRawHtmlWithAttributes()
    {
        var html = _renderer.Render("""Before <span class="x">here</span> after.""");

        Assert.Contains("""&lt;span class=&quot;x&quot;&gt;here""", html);
        Assert.Contains("""<span class="audio-tag">&lt;/span&gt;</span>""", html);
    }
}
