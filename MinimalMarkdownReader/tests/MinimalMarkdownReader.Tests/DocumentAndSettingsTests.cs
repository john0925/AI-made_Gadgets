using System.Text;
using MinimalMarkdownReader.Models;
using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class DocumentAndSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MarkdownReaderTests", Guid.NewGuid().ToString("N"));
    public DocumentAndSettingsTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task LoadAsync_ReadsUtf8Markdown()
    {
        var path = Path.Combine(_directory, "繁中.md");
        await File.WriteAllTextAsync(path, "# 測試文件", new UTF8Encoding(true));
        var document = await new DocumentService().LoadAsync(path);
        Assert.Equal("# 測試文件", document.Content);
    }

    [Fact]
    public async Task LoadAsync_RejectsUnsupportedExtension()
    {
        var path = Path.Combine(_directory, "note.txt");
        await File.WriteAllTextAsync(path, "text");
        await Assert.ThrowsAsync<ArgumentException>(() => new DocumentService().LoadAsync(path));
    }

    [Fact]
    public void TryResolveRelativeImage_AllowsOnlyFilesBelowDocumentDirectory()
    {
        var image = Path.Combine(_directory, "image.png");
        File.WriteAllBytes(image, []);
        var document = new MarkdownDocument(Path.Combine(_directory, "note.md"), _directory, "");
        var service = new DocumentService();
        Assert.True(service.TryResolveRelativeImage(document, "image.png", out var mapped));
        Assert.Equal("image.png", mapped);
        Assert.False(service.TryResolveRelativeImage(document, "../secret.png", out _));
        Assert.False(service.TryResolveRelativeImage(document, "https://example.com/x.png", out _));
    }

    [Fact]
    public async Task SettingsService_InvalidJsonFallsBackToDefaults()
    {
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{ broken");
        var settings = await new SettingsService(path).LoadAsync();
        Assert.Equal(ReaderSettings.DefaultFontSize, settings.FontSize);
    }

    [Fact]
    public async Task SettingsService_SaveThenLoad_PreservesGlobalReadingPreferences()
    {
        var path = Path.Combine(_directory, "settings.json");
        var expected = new ReaderSettings
        {
            Theme = "eye",
            FontFamily = "Microsoft JhengHei",
            FontSize = 24,
            LineHeight = 2.1,
            TextAlign = "center",
            UiLanguage = "en",
            SplitMode = "horizontal",
            SplitRatio = 0.3
        };
        var service = new SettingsService(path);
        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();
        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.FontFamily, actual.FontFamily);
        Assert.Equal(expected.FontSize, actual.FontSize);
        Assert.Equal(expected.LineHeight, actual.LineHeight);
        Assert.Equal(expected.TextAlign, actual.TextAlign);
        Assert.Equal(expected.UiLanguage, actual.UiLanguage);
        Assert.Equal(expected.SplitMode, actual.SplitMode);
        Assert.Equal(expected.SplitRatio, actual.SplitRatio);
    }

    [Theory]
    [InlineData("none", 0.5, true)]
    [InlineData("vertical", 0.15, true)]
    [InlineData("horizontal", 0.85, true)]
    [InlineData("diagonal", 0.5, false)]
    [InlineData("vertical", 0.1, false)]
    [InlineData("vertical", 0.9, false)]
    [InlineData("vertical", double.NaN, false)]
    public void Validate_ChecksSplitModeAndRatio(string mode, double ratio, bool valid) =>
        Assert.Equal(valid, SettingsService.Validate(new ReaderSettings { SplitMode = mode, SplitRatio = ratio }));

    [Fact]
    public async Task SettingsService_LoadOlderFileWithoutSplitFields_UsesSingleView()
    {
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """{ "Theme": "dark" }""");
        var settings = await new SettingsService(path).LoadAsync();
        Assert.Equal(("dark", "none", ReaderSettings.DefaultSplitRatio), (settings.Theme, settings.SplitMode, settings.SplitRatio));
    }

    [Fact]
    public void Validate_RejectsUnsafeColorAndAlignment()
    {
        var settings = new ReaderSettings { TextAlign = "justify" };
        settings.CustomColors.Background = "url(javascript:bad)";
        Assert.False(SettingsService.Validate(settings));
    }

    [Fact]
    public void Validate_AcceptsBuiltInAndCustomThemesOnly()
    {
        foreach (var theme in ThemePalettes.BuiltIn.Keys.Append(ThemePalettes.Custom))
            Assert.True(SettingsService.Validate(new ReaderSettings { Theme = theme }));
        Assert.False(SettingsService.Validate(new ReaderSettings { Theme = "neon" }));
    }

    [Fact]
    public void CustomColorDefaults_MatchSoftPalette()
    {
        var colors = new CustomColors();
        Assert.Equal(ThemePalettes.Soft, new ThemePalette(colors.Background, colors.Text, colors.Heading, colors.Link, colors.CodeBackground));
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
