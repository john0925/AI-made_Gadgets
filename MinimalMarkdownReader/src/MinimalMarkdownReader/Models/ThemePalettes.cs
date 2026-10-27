namespace MinimalMarkdownReader.Models;

public sealed record ThemePalette(string Background, string Text, string Heading, string Link, string CodeBackground);

public static class ThemePalettes
{
    public const string Custom = "custom";

    public static ThemePalette Soft { get; } = new("#F3E9D6", "#352B23", "#352B23", "#245C91", "#E8DCC8");

    public static IReadOnlyDictionary<string, ThemePalette> BuiltIn { get; } = new Dictionary<string, ThemePalette>
    {
        ["soft"] = Soft,
        ["eye"] = new("#101820", "#E8F5C6", "#E8F5C6", "#A7D8FF", "#1D3039"),
        ["light"] = new("#FFFFFF", "#202124", "#202124", "#245C91", "#F1F3F4"),
        ["dark"] = new("#252525", "#E8EAED", "#FFFFFF", "#8AB4F8", "#353535")
    };

    public static bool IsValidTheme(string? theme) => theme == Custom || (theme is not null && BuiltIn.ContainsKey(theme));
}
