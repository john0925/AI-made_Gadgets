using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class FontNamesTests
{
    private static readonly Dictionary<string, string> JhengHei = new() { ["en-us"] = "Microsoft JhengHei", ["zh-tw"] = "微軟正黑體" };
    private static readonly Dictionary<string, string> YaHei = new() { ["en-us"] = "Microsoft YaHei", ["zh-cn"] = "微软雅黑" };
    private static readonly Dictionary<string, string> Arial = new() { ["en-us"] = "Arial" };

    [Theory]
    [InlineData("zh-Hant", "微軟正黑體")]
    [InlineData("en", "Microsoft JhengHei")]
    public void DisplayName_UsesUiLanguage(string language, string expected) =>
        Assert.Equal(expected, FontNames.DisplayName("Microsoft JhengHei", JhengHei, language));

    [Fact]
    public void DisplayName_FallsBackToOtherChineseThenEnglish()
    {
        Assert.Equal("微软雅黑", FontNames.DisplayName("Microsoft YaHei", YaHei, "zh-Hant"));
        Assert.Equal("Arial", FontNames.DisplayName("Arial", Arial, "zh-Hant"));
    }

    [Fact]
    public void DisplayName_MatchesTagsIgnoringCaseAndFallsBackToSource()
    {
        Assert.Equal("新細明體", FontNames.DisplayName("PMingLiU", new Dictionary<string, string> { ["ZH-TW"] = "新細明體" }, "zh-Hant"));
        Assert.Equal("Custom", FontNames.DisplayName("Custom", new Dictionary<string, string> { ["zh-tw"] = " " }, "zh-Hant"));
        Assert.Equal("Custom", FontNames.DisplayName("Custom", new Dictionary<string, string>(), "en"));
    }

    [Fact]
    public void Build_KeepsSourceAndSortsByDisplayName()
    {
        var fonts = FontNames.Build([("Microsoft JhengHei", JhengHei), ("Arial", Arial)], "en");
        Assert.Equal(["Arial", "Microsoft JhengHei"], fonts.Select(font => font.DisplayName));

        var chinese = FontNames.Build([("Microsoft JhengHei", JhengHei)], "zh-Hant").Single();
        Assert.Equal(new FontChoice("Microsoft JhengHei", "微軟正黑體"), chinese);
        Assert.Equal("微軟正黑體", chinese.ToString());
    }
}
