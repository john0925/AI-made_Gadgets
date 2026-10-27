using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class MarkdownServiceTests
{
    [Fact]
    public void ToSafeHtml_DisablesRawHtmlAndUnsafeLinks()
    {
        var root = Path.GetTempPath();
        var document = new MarkdownDocument(Path.Combine(root, "note.md"), root, "<script>alert(1)</script>\n\n[jump](javascript:alert(1))\n\n[safe](https://example.com)");
        var html = new MarkdownService().ToSafeHtml(document, new DocumentService());
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com/", html, StringComparison.OrdinalIgnoreCase);
    }
}
