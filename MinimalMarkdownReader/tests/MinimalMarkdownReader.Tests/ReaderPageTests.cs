using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class ReaderPageTests
{
    [Fact]
    public void Build_IncludesRestrictiveContentSecurityPolicy()
    {
        var html = ReaderPage.Build("<p>body</p>");
        Assert.Contains("http-equiv=\"Content-Security-Policy\"", html);
        Assert.Contains("default-src 'none'", ReaderPage.ContentSecurityPolicy);
        Assert.Contains("img-src https://markdown.localhost;", ReaderPage.ContentSecurityPolicy);
        Assert.DoesNotMatch(new Regex("script-src[^;]*unsafe-inline"), ReaderPage.ContentSecurityPolicy);
    }

    [Fact]
    public void Build_ScriptHashMatchesInlineScript()
    {
        var html = ReaderPage.Build("<p>body</p>");
        var script = Regex.Match(html, "<script>(?<s>.*)</script>", RegexOptions.Singleline).Groups["s"].Value;
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        Assert.Contains($"script-src 'sha256-{hash}'", ReaderPage.ContentSecurityPolicy);
    }

    [Fact]
    public void Build_AppliesAlignmentToWholeDocumentExceptCode()
    {
        var html = ReaderPage.Build("<p>body</p>");
        Assert.Contains("main{text-align:var(--align)}", html);
        Assert.Contains("[data-align=right] table{margin-left:auto}", html);
        Assert.Contains("[data-align=right] :is(ul,ol){list-style-position:inside", html.Replace("[data-align=center] :is(ul,ol),", ""));
        Assert.Contains("r.dataset.align=s.textAlign", html);
        Assert.Matches("pre\\{[^}]*text-align:left", html);
    }

    [Theory]
    [InlineData("https://markdown.localhost/images/a.png", true, true)]
    [InlineData("https://markdown.localhost/notes.txt", false, false)]
    [InlineData("https://evil.example/track.gif", true, false)]
    [InlineData("http://markdown.localhost/a.png", true, false)]
    [InlineData("file:///C:/Windows/win.ini", true, false)]
    [InlineData("wss://evil.example/socket", false, false)]
    public void IsAllowedResource_OnlyAllowsDocumentFolderImages(string uri, bool isImage, bool expected)
    {
        Assert.Equal(expected, ReaderPage.IsAllowedResource(uri, isImage));
    }

    [Theory]
    [InlineData("data:text/html;charset=utf-8,<p>x</p>", true)]
    [InlineData("about:blank", true)]
    [InlineData("file:///C:/", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("ms-settings:", false)]
    public void IsDocumentNavigation_AcceptsOnlyInlineDocuments(string uri, bool expected)
    {
        Assert.Equal(expected, ReaderPage.IsDocumentNavigation(uri));
    }
}
