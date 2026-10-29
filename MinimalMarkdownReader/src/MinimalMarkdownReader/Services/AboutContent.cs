using System.Text.RegularExpressions;

namespace MinimalMarkdownReader.Services;

public sealed record AboutSegment(string Text, Uri? Link);

public sealed record AboutLine(bool IsHeading, IReadOnlyList<AboutSegment> Segments);

// Splits the About text into lines: "# " lines become headings and HTTP(S) URLs become links.
public static class AboutContent
{
    private static readonly Regex UrlPattern = new(@"https?://[\x21-\x7E-[<>""']]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', ')'];

    public static IReadOnlyList<AboutLine> Parse(string text)
    {
        var lines = new List<AboutLine>();
        foreach (var rawLine in text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
        {
            var isHeading = rawLine.StartsWith("# ", StringComparison.Ordinal);
            lines.Add(new AboutLine(isHeading, ParseSegments(isHeading ? rawLine[2..] : rawLine)));
        }
        return lines;
    }

    private static List<AboutSegment> ParseSegments(string line)
    {
        var segments = new List<AboutSegment>();
        var position = 0;
        foreach (Match match in UrlPattern.Matches(line))
        {
            var url = match.Value.TrimEnd(TrailingPunctuation);
            if (!ExternalLinks.TryGetWebUri(url, out var uri)) continue;
            if (match.Index > position) segments.Add(new AboutSegment(line[position..match.Index], null));
            segments.Add(new AboutSegment(url, uri));
            position = match.Index + url.Length;
        }
        if (position < line.Length) segments.Add(new AboutSegment(line[position..], null));
        return segments;
    }
}
