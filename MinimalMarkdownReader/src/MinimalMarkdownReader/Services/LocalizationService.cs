using System.Globalization;
using System.Resources;

namespace MinimalMarkdownReader.Services;

public sealed class LocalizationService
{
    private static readonly ResourceManager ResourceManager = new(
        "MinimalMarkdownReader.Resources.Strings", typeof(LocalizationService).Assembly);

    public static LocalizationService Instance { get; } = new();

    public string CurrentLanguage { get; private set; } = "en";

    public string SetLanguage(string? requestedLanguage)
    {
        CurrentLanguage = requestedLanguage is "zh-Hant" or "en"
            ? requestedLanguage
            : CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-Hant" : "en";
        return CurrentLanguage;
    }

    public string Get(string key) => ResourceManager.GetString(key, CultureInfo.GetCultureInfo(CurrentLanguage)) ?? key;
    public string Format(string key, params object[] values) => string.Format(CultureInfo.GetCultureInfo(CurrentLanguage), Get(key), values);
}
