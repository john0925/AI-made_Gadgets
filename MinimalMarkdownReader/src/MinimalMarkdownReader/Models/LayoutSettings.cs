namespace MinimalMarkdownReader.Models;

// Per-tab reading layout. Theme, colors and UI language stay global in ReaderSettings.
public sealed record LayoutSettings(string TextAlign, string FontFamily, int FontSize, double LineHeight);
