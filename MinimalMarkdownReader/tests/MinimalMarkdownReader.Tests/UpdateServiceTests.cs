using System.Runtime.CompilerServices;
using System.Text.Json;
using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public void FindLatestVersion_PicksHighestPublishedTagOfThisApp()
    {
        const string json = """
            [
              { "tag_name": "OtherGadget-v9.0.0", "draft": false, "prerelease": false },
              { "tag_name": "MinimalMarkdownReader-v1.1.1", "draft": false, "prerelease": false },
              { "tag_name": "MinimalMarkdownReader-v1.10.0", "draft": false, "prerelease": false },
              { "tag_name": "MinimalMarkdownReader-v2.0.0", "draft": true, "prerelease": false },
              { "tag_name": "MinimalMarkdownReader-v3.0.0", "draft": false, "prerelease": true },
              { "tag_name": "MinimalMarkdownReader-v1.2", "draft": false, "prerelease": false },
              { "tag_name": "MinimalMarkdownReader-v1.2.0-beta", "draft": false, "prerelease": false },
              { "tag_name": 5 },
              "junk"
            ]
            """;
        Assert.Equal(new Version(1, 10, 0), UpdateService.FindLatestVersion(json));
    }

    [Fact]
    public void FindLatestVersion_ReturnsNullWithoutMatchingRelease() =>
        Assert.Null(UpdateService.FindLatestVersion("""[{ "tag_name": "OtherGadget-v1.0.0" }]"""));

    [Theory]
    [InlineData("""{ "message": "API rate limit exceeded" }""")]
    [InlineData("not json")]
    public void FindLatestVersion_RejectsUnexpectedResponses(string json) =>
        Assert.ThrowsAny<JsonException>(() => UpdateService.FindLatestVersion(json));

    [Theory]
    [InlineData("MinimalMarkdownReader-v1.1.1", true)]
    [InlineData("MinimalMarkdownReader-v12.0.3", true)]
    [InlineData("minimalmarkdownreader-v1.1.1", false)]
    [InlineData("MinimalMarkdownReader-v1.1", false)]
    [InlineData("MinimalMarkdownReader-v1.1.1.1", false)]
    [InlineData("MinimalMarkdownReader-v1.1.x", false)]
    [InlineData("MinimalMarkdownReader-v 1.1.1", false)]
    [InlineData("MinimalMarkdownReader-v-1.1.1", false)]
    public void TryParseTag_AcceptsOnlyThreePartVersions(string tag, bool expected) =>
        Assert.Equal(expected, UpdateService.TryParseTag(tag, out _));

    [Fact]
    public void ReleasePage_PointsToThisAppsReleaseTag() =>
        Assert.Equal("https://github.com/john0925/AI-made_Gadgets/releases/tag/MinimalMarkdownReader-v1.2.0", UpdateService.ReleasePage(new Version(1, 2, 0)).AbsoluteUri);

    // The csproj <Version> is what "Check for updates" compares against, so it must match the installer and About text.
    [Fact]
    public void CurrentVersion_MatchesInstallerAndAboutText()
    {
        var root = RepositoryRoot();
        var version = UpdateService.CurrentVersion.ToString();
        Assert.Equal(3, version.Split('.').Length);
        Assert.Contains($"#define MyAppVersion \"{version}\"", File.ReadAllText(Path.Combine(root, "installer", "MinimalMarkdownReader.iss")));
        Assert.Contains($"版本 {version}", File.ReadAllText(Path.Combine(root, "src", "MinimalMarkdownReader", "about.md")));
        Assert.Contains($"Version {version}", File.ReadAllText(Path.Combine(root, "src", "MinimalMarkdownReader", "about.en.md")));
    }

    // Found from this source file's path, so it works wherever the test binaries are built.
    private static string RepositoryRoot([CallerFilePath] string sourceFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", ".."));
}
