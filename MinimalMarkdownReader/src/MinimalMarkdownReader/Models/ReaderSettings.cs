namespace MinimalMarkdownReader.Models;

public sealed class ReaderSettings
{
    public const int DefaultFontSize = 18;
    public const double DefaultLineHeight = 1.8;

    public string FontFamily { get; set; } = "Microsoft JhengHei";
    public int FontSize { get; set; } = DefaultFontSize;
    public double LineHeight { get; set; } = DefaultLineHeight;
    public string TextAlign { get; set; } = "left";
    public string Theme { get; set; } = "soft";
    public string UiLanguage { get; set; } = "";
    public CustomColors CustomColors { get; set; } = new();

    public ReaderSettings Clone() => new()
    {
        FontFamily = FontFamily,
        FontSize = FontSize,
        LineHeight = LineHeight,
        TextAlign = TextAlign,
        Theme = Theme,
        UiLanguage = UiLanguage,
        CustomColors = CustomColors.Clone()
    };
}

public sealed class CustomColors
{
    public string Background { get; set; } = ThemePalettes.Soft.Background;
    public string Text { get; set; } = ThemePalettes.Soft.Text;
    public string Heading { get; set; } = ThemePalettes.Soft.Heading;
    public string Link { get; set; } = ThemePalettes.Soft.Link;
    public string CodeBackground { get; set; } = ThemePalettes.Soft.CodeBackground;

    public CustomColors Clone() => (CustomColors)MemberwiseClone();
}
