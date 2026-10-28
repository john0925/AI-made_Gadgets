using System.Text.RegularExpressions;
using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class MarkdownServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MarkdownReaderTests", Guid.NewGuid().ToString("N"));
    public MarkdownServiceTests() => Directory.CreateDirectory(_directory);

    private string Render(string markdown) =>
        new MarkdownService().ToSafeHtml(new MarkdownDocument(Path.Combine(_directory, "note.md"), _directory, markdown), new DocumentService());

    [Fact]
    public void ToSafeHtml_DisablesRawHtmlAndUnsafeLinks()
    {
        var html = Render("<script>alert(1)</script>\n\n[jump](javascript:alert(1))\n\n[safe](https://example.com)");
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("# Title {onmouseover=\"alert(1)\"}")]
    [InlineData("[ok](https://example.com){onclick=\"alert(1)\"}")]
    [InlineData("![a](ok.png){onload=\"alert(1)\"}")]
    [InlineData("Para {style=\"background:url(https://evil.example/t.gif)\"}")]
    public void ToSafeHtml_IgnoresGenericAttributes(string markdown)
    {
        File.WriteAllBytes(Path.Combine(_directory, "ok.png"), []);
        var html = Render(markdown);
        // The braces may remain as visible text, but never as an attribute inside a tag.
        Assert.DoesNotMatch(new Regex(@"<[^>]+\s(on\w+|style)\s*=", RegexOptions.IgnoreCase), html);
    }

    [Theory]
    [InlineData("![v](https://www.youtube.com/watch?v=dQw4w9WgXcQ)")]
    [InlineData("![v](https://evil.example/movie.mp4)")]
    public void ToSafeHtml_DoesNotEmbedRemoteMedia(string markdown)
    {
        var html = Render(markdown);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<video", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blocked-resource", html);
    }

    [Fact]
    public void ToSafeHtml_KeepsCommonExtensions()
    {
        var html = Render("| a | b |\n|---|---|\n| 1 | 2 |\n\n- [x] done\n\nText[^1]\n\n[^1]: Note");
        Assert.Contains("<table>", html);
        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("footnotes", html);
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
