using System.Globalization;

namespace MinimalMarkdownReader.Services;

// A font in the toolbar list. Source (the English family name) is what settings and CSS use; DisplayName is shown.
public sealed record FontChoice(string Source, string DisplayName)
{
    public override string ToString() => DisplayName;
}

// Picks each font's display name for the UI language from the names stored in the font, keyed by IETF tag.
public static class FontNames
{
    private static readonly string[] ChineseTags = ["zh-tw", "zh-hant", "zh-hk", "zh-mo", "zh-cn", "zh-hans", "zh-sg", "en-us"];
    private static readonly string[] EnglishTags = ["en-us"];

    public static string DisplayName(string source, IReadOnlyDictionary<string, string> namesByTag, string language)
    {
        foreach (var tag in language == "zh-Hant" ? ChineseTags : EnglishTags)
            foreach (var (key, name) in namesByTag)
                if (key.Equals(tag, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(name)) return name;
        return source;
    }

    public static IReadOnlyList<FontChoice> Build(IEnumerable<(string Source, IReadOnlyDictionary<string, string> NamesByTag)> fonts, string language)
    {
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo(language == "zh-Hant" ? "zh-TW" : "en-US"), ignoreCase: true);
        return fonts.Select(font => new FontChoice(font.Source, DisplayName(font.Source, font.NamesByTag, language)))
            .OrderBy(font => font.DisplayName, comparer)
            .ToList();
    }
}
