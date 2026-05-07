using AgentRp.Services;

namespace AgentRp.Tests;

public sealed class TranscriptBodyRendererTests
{
    readonly TranscriptBodyRenderer _renderer = new(new MarkdownRenderer());

    [Fact]
    public void RenderMarksSquareAudioTags()
    {
        var html = _renderer.Render("[whispers] \"Stay close.\"");

        Assert.Contains("""<span class="audio-tag">[whispers]</span>""", html);
        Assert.Contains("Stay close.", html);
    }

    [Fact]
    public void RenderMarksXAiSpeechTagsAsVisibleText()
    {
        var html = _renderer.Render("<whisper>Stay close.</whisper>");

        Assert.Contains("""<span class="audio-tag">&lt;whisper&gt;</span>""", html);
        Assert.Contains("Stay close.", html);
        Assert.Contains("""<span class="audio-tag">&lt;/whisper&gt;</span>""", html);
    }

    [Fact]
    public void RenderMarksExtendedXAiWrappingSpeechTagsAsVisibleText()
    {
        var html = _renderer.Render("<build-intensity>Stay close.</build-intensity>");

        Assert.Contains("""<span class="audio-tag">&lt;build-intensity&gt;</span>""", html);
        Assert.Contains("Stay close.", html);
        Assert.Contains("""<span class="audio-tag">&lt;/build-intensity&gt;</span>""", html);
    }

    [Fact]
    public void MarkAudioTagsDoesNotRewriteMarkdownLinks()
    {
        var markdown = TranscriptBodyRenderer.MarkAudioTags("[docs](https://example.test)");

        Assert.Equal("[docs](https://example.test)", markdown);
    }
}
