using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class AboutContentTests
{
    [Fact]
    public void Parse_MarksHeadingAndKeepsBlankLines()
    {
        var lines = AboutContent.Parse("# MinimalMarkdownReader\r\n\r\nText\r\n");
        Assert.Equal(3, lines.Count);
        Assert.True(lines[0].IsHeading);
        Assert.Equal("MinimalMarkdownReader", Assert.Single(lines[0].Segments).Text);
        Assert.Empty(lines[1].Segments);
        Assert.False(lines[2].IsHeading);
    }

    [Theory]
    [InlineData("GitHub：https://github.com/john0925/AI-made_Gadgets", "GitHub：", "https://github.com/john0925/AI-made_Gadgets")]
    [InlineData("Latest release: https://github.com/john0925/AI-made_Gadgets/releases.", "Latest release: ", "https://github.com/john0925/AI-made_Gadgets/releases")]
    public void Parse_TurnsUrlsIntoLinks(string line, string label, string url)
    {
        var segments = AboutContent.Parse(line)[0].Segments;
        Assert.Equal(label, segments[0].Text);
        Assert.Null(segments[0].Link);
        Assert.Equal(url, segments[1].Text);
        Assert.Equal(new Uri(url), segments[1].Link);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("ftp://example.com/file")]
    public void Parse_DoesNotLinkNonWebSchemes(string line)
    {
        Assert.All(AboutContent.Parse(line)[0].Segments, segment => Assert.Null(segment.Link));
    }

    [Theory]
    [InlineData("about.md")]
    [InlineData("about.en.md")]
    public void EmbeddedAbout_HasHeadingAndClickableGitHubLinks(string resourceName)
    {
        var language = resourceName == "about.en.md" ? "en" : "zh-Hant";
        var lines = AboutContent.Parse(LocalizationService.ReadLocalizedResource("about", ".md", language)!);
        Assert.True(lines[0].IsHeading);
        var links = lines.SelectMany(line => line.Segments).Where(segment => segment.Link is not null).ToList();
        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.StartsWith("https://github.com/john0925/AI-made_Gadgets", link.Link!.AbsoluteUri));
    }

    [Theory]
    [InlineData("https://example.com/", true)]
    [InlineData("http://example.com/", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/", false)]
    [InlineData("not a url", false)]
    public void ExternalLinks_TryGetWebUri_AcceptsOnlyHttp(string value, bool expected)
    {
        Assert.Equal(expected, ExternalLinks.TryGetWebUri(value, out _));
    }
}
