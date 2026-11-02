using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

namespace MinimalMarkdownReader.Services;

// Checks GitHub Releases for a newer version, only when the user asks. The repository also holds other projects'
// releases, so only tags named "MinimalMarkdownReader-v<version>" count. Only the version number is read from the
// response; the download page URL is built here, never taken from it.
public static class UpdateService
{
    public const string TagPrefix = "MinimalMarkdownReader-v";
    private const string ReleasesApi = "https://api.github.com/repos/john0925/AI-made_Gadgets/releases?per_page=100";
    private const string ReleasePageBase = "https://github.com/john0925/AI-made_Gadgets/releases/tag/";

    public static Version CurrentVersion { get; } = Normalize(typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0));

    public static Uri ReleasePage(Version version) => new(ReleasePageBase + TagPrefix + version);

    // Throws HttpRequestException, TaskCanceledException (timeout) or JsonException when the check fails.
    public static async Task<Version?> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MinimalMarkdownReader", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        var json = await client.GetStringAsync(ReleasesApi, cancellationToken);
        return FindLatestVersion(json);
    }

    // The highest published (not draft, not prerelease) version among this app's tags, or null when there is none.
    public static Version? FindLatestVersion(string releasesJson)
    {
        using var document = JsonDocument.Parse(releasesJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected a list of releases.");
        Version? latest = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object || IsTrue(release, "draft") || IsTrue(release, "prerelease")) continue;
            if (!release.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String) continue;
            if (TryParseTag(tag.GetString()!, out var version) && (latest is null || version > latest)) latest = version;
        }
        return latest;
    }

    public static bool TryParseTag(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal)) return false;
        var text = tag[TagPrefix.Length..];
        if (text.Split('.').Length != 3 || text.Any(c => c != '.' && !char.IsAsciiDigit(c)) || !Version.TryParse(text, out var parsed)) return false;
        version = parsed;
        return true;
    }

    private static bool IsTrue(JsonElement release, string name) =>
        release.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
