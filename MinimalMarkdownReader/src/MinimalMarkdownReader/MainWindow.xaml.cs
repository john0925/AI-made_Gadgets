using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using MinimalMarkdownReader.Models;
using MinimalMarkdownReader.Services;
using Microsoft.Win32;

namespace MinimalMarkdownReader;

public partial class MainWindow : Window
{
    private readonly DocumentService _documentService = new();
    private readonly MarkdownService _markdownService = new();
    private readonly SettingsService _settingsService = new();
    private readonly LocalizationService _localization = LocalizationService.Instance;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private ReaderService? _readerService;
    private ReaderSettings _settings = new();
    private bool _updatingControls;
    private bool _controlsReady;
    private bool _isClosing;
    private string? _currentDocumentName;
    private readonly string? _initialDocumentPath;

    public MainWindow(string? initialDocumentPath = null)
    {
        _initialDocumentPath = initialDocumentPath;
        InitializeComponent();
        _controlsReady = true;
        _saveTimer.Tick += SaveTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FontFamilyCombo.ItemsSource = Fonts.SystemFontFamilies.OrderBy(font => font.Source);
        _settings = await _settingsService.LoadAsync();
        _settings.UiLanguage = _localization.SetLanguage(_settings.UiLanguage);
        ApplyLocalizedText();
        SetControlsFromSettings();
        _readerService = new ReaderService(ReaderWebView);
        _readerService.NavigationFailed += (_, message) => StatusText.Text = message;
        try { await _readerService.InitializeAsync(); }
        catch (Exception ex) { StatusText.Text = _localization.Format("WebViewInitializationFailed", ex.Message); }

        if (!string.IsNullOrWhiteSpace(_initialDocumentPath))
        {
            await OpenDocumentAsync(_initialDocumentPath);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing) return;
        e.Cancel = true;
        _saveTimer.Stop();
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, _localization.Format("SaveSettingsFailed", ex.Message), _localization.Get("SaveSettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isClosing = true;
            Close();
        }
    }

    private async void OpenDocument_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = _localization.Get("FileDialogFilter"), Multiselect = false };
        if (dialog.ShowDialog(this) == true) await OpenDocumentAsync(dialog.FileName);
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) await OpenDocumentAsync(paths[0]);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async Task OpenDocumentAsync(string path)
    {
        if (_readerService is null) { StatusText.Text = _localization.Get("ReaderInitializing"); return; }
        try
        {
            var document = await _documentService.LoadAsync(path);
            var html = _markdownService.ToSafeHtml(document, _documentService);
            WelcomePanel.Visibility = Visibility.Collapsed;
            ReaderWebView.Visibility = Visibility.Visible;
            _readerService.ShowDocument(document, html, _settings);
            _currentDocumentName = Path.GetFileName(document.Path);
            StatusText.Text = _localization.Format("ReadingDocument", _currentDocumentName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, _localization.Get("OpenFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = _localization.Get("OpenFailed");
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { OpenDocument_Click(sender, e); e.Handled = true; }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (MenuButton.ContextMenu is null) return;
        MenuButton.ContextMenu.PlacementTarget = MenuButton;
        MenuButton.ContextMenu.Placement = PlacementMode.Bottom;
        MenuButton.ContextMenu.IsOpen = true;
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string language }) return;
        _settings.UiLanguage = _localization.SetLanguage(language);
        ApplyLocalizedText();
        ApplyAndQueueSave();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var content = LocalizationService.ReadLocalizedResource("about", ".md", _localization.CurrentLanguage);
        if (content is null)
            MessageBox.Show(this, _localization.Get("AboutFileUnavailable"), _localization.Get("About"), MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            MessageBox.Show(this, content, _localization.Get("About"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e) => UpdateAlignment("left");
    private void AlignCenter_Click(object sender, RoutedEventArgs e) => UpdateAlignment("center");
    private void AlignRight_Click(object sender, RoutedEventArgs e) => UpdateAlignment("right");
    private void UpdateAlignment(string alignment) { _settings.TextAlign = alignment; ApplyAndQueueSave(); }

    private void SettingsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady || _updatingControls) return;
        if (sender == FontFamilyCombo && FontFamilyCombo.SelectedItem is FontFamily font) _settings.FontFamily = font.Source;
        _settings.FontSize = (int)Math.Round(FontSizeSlider.Value);
        _settings.LineHeight = Math.Round(LineHeightSlider.Value, 1);
        if (ThemeCombo.SelectedItem is ComboBoxItem theme) _settings.Theme = (string)theme.Tag;
        FontSizeText.Text = _settings.FontSize.ToString();
        LineHeightText.Text = _settings.LineHeight.ToString("0.0");
        UpdateCustomColorControls();
        ApplyAndQueueSave();
    }

    private void BackgroundColor_Click(object sender, RoutedEventArgs e) => ChooseCustomColor(isBackground: true);
    private void TextColor_Click(object sender, RoutedEventArgs e) => ChooseCustomColor(isBackground: false);

    private void ChooseCustomColor(bool isBackground)
    {
        var currentColor = isBackground ? _settings.CustomColors.Background : _settings.CustomColors.Text;
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.ColorTranslator.FromHtml(currentColor)
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        if (isBackground) _settings.CustomColors.Background = hex;
        else _settings.CustomColors.Text = hex;
        UpdateCustomColorControls();
        ApplyAndQueueSave();
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        _settings = new ReaderSettings { UiLanguage = _localization.CurrentLanguage };
        SetControlsFromSettings();
        ApplyAndQueueSave();
    }

    private async void SaveTimer_Tick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        try { await _settingsService.SaveAsync(_settings); }
        catch (Exception ex) { StatusText.Text = _localization.Format("SaveSettingsFailed", ex.Message); }
    }

    private async void ApplyAndQueueSave()
    {
        if (_readerService is not null && ReaderWebView.Visibility == Visibility.Visible) await _readerService.ApplySettingsAsync(_settings);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SetControlsFromSettings()
    {
        _updatingControls = true;
        FontFamilyCombo.SelectedItem = ((IEnumerable<FontFamily>)FontFamilyCombo.ItemsSource).FirstOrDefault(font => font.Source.Equals(_settings.FontFamily, StringComparison.OrdinalIgnoreCase));
        FontSizeSlider.Value = _settings.FontSize;
        LineHeightSlider.Value = _settings.LineHeight;
        SetThemeSelection();
        FontSizeText.Text = _settings.FontSize.ToString();
        LineHeightText.Text = _settings.LineHeight.ToString("0.0");
        _updatingControls = false;
        UpdateCustomColorControls();
    }

    private void SetThemeSelection()
    {
        ThemeCombo.SelectedItem = ThemeCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == _settings.Theme);
    }

    private void UpdateCustomColorControls()
    {
        CustomColorPanel.Visibility = _settings.Theme == "custom" ? Visibility.Visible : Visibility.Collapsed;
        BackgroundColorButton.Content = _settings.CustomColors.Background;
        BackgroundColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.CustomColors.Background));
        TextColorButton.Content = _settings.CustomColors.Text;
        TextColorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.CustomColors.Text));
        TextColorButton.Foreground = new SolidColorBrush(ContrastColor(_settings.CustomColors.Text));
    }

    private static Color ContrastColor(string value)
    {
        var color = (Color)ColorConverter.ConvertFromString(value);
        return color.R * 0.299 + color.G * 0.587 + color.B * 0.114 > 150 ? Colors.Black : Colors.White;
    }

    private void ApplyLocalizedText()
    {
        Title = "MinimalMarkdownReader";
        MenuButton.ToolTip = _localization.Get("Menu");
        MenuOpenDocumentItem.Header = _localization.Get("OpenDocument");
        MenuLanguageItem.Header = _localization.Get("ChangeLanguage");
        MenuTraditionalChineseItem.Header = _localization.Get("TraditionalChinese");
        MenuTraditionalChineseItem.IsChecked = _localization.CurrentLanguage == "zh-Hant";
        MenuEnglishItem.Header = _localization.Get("English");
        MenuEnglishItem.IsChecked = _localization.CurrentLanguage == "en";
        MenuAboutItem.Header = _localization.Get("About");
        TextAlignmentLabel.Text = _localization.Get("TextAlignment");
        AlignLeftButton.ToolTip = _localization.Get("AlignLeft");
        AlignCenterButton.ToolTip = _localization.Get("AlignCenter");
        AlignRightButton.ToolTip = _localization.Get("AlignRight");
        FontLabel.Text = _localization.Get("Font");
        FontSizeLabel.Text = _localization.Get("FontSize");
        LineHeightLabel.Text = _localization.Get("LineHeight");
        ThemeLabel.Text = _localization.Get("Theme");
        ThemeSoftItem.Content = _localization.Get("ThemeSoft");
        ThemeEyeItem.Content = _localization.Get("ThemeEye");
        ThemeLightItem.Content = _localization.Get("ThemeLight");
        ThemeDarkItem.Content = _localization.Get("ThemeDark");
        ThemeCustomItem.Content = _localization.Get("ThemeCustom");
        BackgroundColorLabel.Text = _localization.Get("BackgroundColor");
        DocumentColorLabel.Text = _localization.Get("DocumentColor");
        RestoreDefaultsButton.Content = _localization.Get("RestoreDefaults");
        WelcomeInstructionText.Text = _localization.Get("WelcomeInstruction");
        WelcomeOpenButton.Content = _localization.Get("OpenMarkdownDocument");
        StatusText.Text = _currentDocumentName is null ? _localization.Get("NoDocument") : _localization.Format("ReadingDocument", _currentDocumentName);
    }
}
