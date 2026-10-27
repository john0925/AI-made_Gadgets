using System.Text.Json;
using System.Text.RegularExpressions;
using MinimalMarkdownReader.Models;

namespace MinimalMarkdownReader.Services;

public sealed class SettingsService
{
    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private readonly string _settingsPath;
    private readonly string? _legacySettingsPath;

    public SettingsService(string? settingsPath = null)
    {
        if (settingsPath is not null)
        {
            _settingsPath = settingsPath;
            return;
        }
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(localAppData, "MinimalMarkdownReader", "settings.json");
        _legacySettingsPath = Path.Combine(localAppData, "MarkdownReader", "settings.json");
    }

    public async Task<ReaderSettings> LoadAsync()
    {
        var path = File.Exists(_settingsPath) ? _settingsPath : _legacySettingsPath;
        if (path is null || !File.Exists(path)) return new ReaderSettings();
        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<ReaderSettings>(stream);
            return Validate(settings) ? settings! : new ReaderSettings();
        }
        catch (IOException) { return new ReaderSettings(); }
        catch (JsonException) { return new ReaderSettings(); }
    }

    public async Task SaveAsync(ReaderSettings settings)
    {
        if (!Validate(settings)) throw new ArgumentException(LocalizationService.Instance.Get("InvalidSettings"), nameof(settings));
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true });
        }
        File.Move(temporaryPath, _settingsPath, true);
    }

    public static bool Validate(ReaderSettings? settings) => settings is not null
        && settings.FontSize is >= 12 and <= 36
        && settings.LineHeight is >= 1.2 and <= 2.5
        && settings.TextAlign is "left" or "center" or "right"
        && ThemePalettes.IsValidTheme(settings.Theme)
        && settings.UiLanguage is "" or "zh-Hant" or "en"
        && !string.IsNullOrWhiteSpace(settings.FontFamily)
        && settings.FontFamily.Length <= 200
        && settings.CustomColors is not null
        && new[] { settings.CustomColors.Background, settings.CustomColors.Text, settings.CustomColors.Heading, settings.CustomColors.Link, settings.CustomColors.CodeBackground }.All(IsColor);

    private static bool IsColor(string? value) => value is not null && ColorPattern.IsMatch(value);
}
