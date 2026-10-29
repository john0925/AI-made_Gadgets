using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void SetLanguage_TraditionalChinese_ReturnsTraditionalChineseResources()
    {
        var localization = LocalizationService.Instance;
        localization.SetLanguage("zh-Hant");
        Assert.Equal("開啟文件", localization.Get("OpenDocument"));
    }

    [Fact]
    public void SetLanguage_English_ReturnsEnglishResources()
    {
        var localization = LocalizationService.Instance;
        localization.SetLanguage("en");
        Assert.Equal("Open document", localization.Get("OpenDocument"));
    }

    [Theory]
    [InlineData("en", "Version")]
    [InlineData("zh-Hant", "版本")]
    [InlineData("fr", "版本")]
    public void ReadLocalizedResource_PicksLanguageAndFallsBackToDefault(string language, string expectedText)
    {
        var content = LocalizationService.ReadLocalizedResource("about", ".md", language);
        Assert.NotNull(content);
        Assert.Contains(expectedText, content);
        Assert.Contains("https://github.com/john0925/AI-made_Gadgets", content);
    }

    [Fact]
    public void ReadLocalizedResource_MissingResourceReturnsNull()
    {
        Assert.Null(LocalizationService.ReadLocalizedResource("missing", ".md", "en"));
    }
}
