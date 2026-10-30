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

    // The four layout fields double as the default layout for newly opened tabs.
    public LayoutSettings GetLayout() => new(TextAlign, FontFamily, FontSize, LineHeight);

    public void SetLayout(LayoutSettings layout)
    {
        TextAlign = layout.TextAlign;
        FontFamily = layout.FontFamily;
        FontSize = layout.FontSize;
        LineHeight = layout.LineHeight;
    }

    // A copy whose layout fields come from a tab; used for what the page actually renders.
    public ReaderSettings WithLayout(LayoutSettings layout)
    {
        var settings = Clone();
        settings.SetLayout(layout);
        return settings;
    }

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
