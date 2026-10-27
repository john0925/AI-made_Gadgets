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
}
