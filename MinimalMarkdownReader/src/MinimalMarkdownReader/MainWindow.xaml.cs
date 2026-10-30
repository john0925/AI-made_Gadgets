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
    private readonly WorkspaceService _workspace = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private ReaderService? _readerService;
    private ReaderSettings _settings = new();
    private bool _updatingControls;
    private bool _controlsReady;
    private bool _isClosing;
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
        _readerService.FileDropped += async (_, path) => await OpenDocumentsAsync([path]);
        try { await _readerService.InitializeAsync(); }
        catch (Exception ex) { StatusText.Text = _localization.Format("WebViewInitializationFailed", ex.Message); }

        if (!string.IsNullOrWhiteSpace(_initialDocumentPath))
        {
            await OpenDocumentsAsync([_initialDocumentPath]);
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
        var dialog = new OpenFileDialog { Filter = _localization.Get("FileDialogFilter"), Multiselect = true };
        if (dialog.ShowDialog(this) == true) await OpenDocumentsAsync(dialog.FileNames);
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) await OpenDocumentsAsync(paths);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool IsDocumentError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException;

    private void ShowOpenError(string message)
    {
        MessageBox.Show(this, message, _localization.Get("OpenFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        StatusText.Text = _localization.Get("OpenFailed");
    }

    // Opens each file in a tab (an already open file reuses its tab) and shows the last one that loaded.
    private async Task OpenDocumentsAsync(IEnumerable<string> paths)
    {
        if (_readerService is null) { StatusText.Text = _localization.Get("ReaderInitializing"); return; }
        MarkdownDocument? lastDocument = null;
        DocumentTab? lastTab = null;
        foreach (var path in paths)
        {
            try
            {
                lastDocument = await _documentService.LoadAsync(path);
                lastTab = _workspace.Open(lastDocument.Path, _settings.GetLayout());
            }
            catch (Exception ex) when (IsDocumentError(ex))
            {
                ShowOpenError(ex.Message);
            }
        }
        if (lastTab is not null && lastDocument is not null) await ShowTabAsync(lastTab, lastDocument);
        else RebuildTabStrip();
    }

    // Reloads the file from disk each time, so switching tabs also picks up edits; a file that can no longer be read closes its tab.
    private async Task ActivateTabAsync(DocumentTab tab)
    {
        if (tab == _workspace.ActiveTab) return;
        try
        {
            await ShowTabAsync(tab, await _documentService.LoadAsync(tab.Path));
        }
        catch (Exception ex) when (IsDocumentError(ex))
        {
            ShowOpenError(ex.Message);
            await CloseTabAsync(tab);
        }
    }

    private async Task ShowTabAsync(DocumentTab tab, MarkdownDocument document)
    {
        await RememberScrollPositionAsync();
        _workspace.Activate(tab);
        var html = _markdownService.ToSafeHtml(document, _documentService);
        WelcomePanel.Visibility = Visibility.Collapsed;
        ReaderWebView.Visibility = Visibility.Visible;
        _readerService!.ShowDocument(document, html, EffectiveSettings, tab.ScrollY);
        SetControlsFromSettings();
        UpdateStatus();
        RebuildTabStrip();
    }

    private async Task RememberScrollPositionAsync()
    {
        if (_workspace.ActiveTab is { } active && _readerService is not null && ReaderWebView.Visibility == Visibility.Visible)
            active.ScrollY = await _readerService.GetScrollPositionAsync();
    }

    private async Task CloseTabAsync(DocumentTab tab)
    {
        var wasActive = tab == _workspace.ActiveTab;
        var next = _workspace.Close(tab);
        if (!wasActive) RebuildTabStrip();
        else if (next is null) ShowWelcome();
        else await ActivateTabAsync(next);
    }

    private async Task CloseOtherTabsAsync(DocumentTab keep)
    {
        _workspace.CloseOthers(keep);
        if (_workspace.ActiveTab == keep) RebuildTabStrip();
        else await ActivateTabAsync(keep);
    }

    private void ShowWelcome()
    {
        ReaderWebView.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Visible;
        SetControlsFromSettings();
        UpdateStatus();
        RebuildTabStrip();
    }

    private void UpdateStatus() =>
        StatusText.Text = _workspace.ActiveTab is { } tab ? _localization.Format("ReadingDocument", tab.Title) : _localization.Get("NoDocument");

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.O && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            OpenDocument_Click(sender, e);
        }
        else if (e.Key == Key.W && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            if (_workspace.ActiveTab is { } active) await CloseTabAsync(active);
        }
        else if (e.Key == Key.Tab && modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            var next = _workspace.Neighbor(modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            if (next is not null) await ActivateTabAsync(next);
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (MenuButton.ContextMenu is null) return;
        MenuSetDefaultLayoutItem.IsEnabled = _workspace.ActiveTab is not null;
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
            new AboutWindow(_localization.Get("About"), content, _localization.Get("Close")) { Owner = this }.ShowDialog();
    }

    // Layout (alignment, font, size, line height) belongs to the active tab; with no tab open the toolbar edits the
    // saved default layout that new tabs start from. Theme and colors are always global.
    private LayoutSettings CurrentLayout => _workspace.ActiveTab?.Layout ?? _settings.GetLayout();

    private void SetCurrentLayout(LayoutSettings layout)
    {
        if (_workspace.ActiveTab is { } tab) tab.Layout = layout;
        else _settings.SetLayout(layout);
    }

    private ReaderSettings EffectiveSettings => _settings.WithLayout(CurrentLayout);

    private void SetDefaultLayout_Click(object sender, RoutedEventArgs e)
    {
        if (_workspace.ActiveTab is not { } tab) return;
        _settings.SetLayout(tab.Layout);
        ApplyAndQueueSave();
        StatusText.Text = _localization.Get("DefaultLayoutSaved");
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e) => UpdateAlignment("left");
    private void AlignCenter_Click(object sender, RoutedEventArgs e) => UpdateAlignment("center");
    private void AlignRight_Click(object sender, RoutedEventArgs e) => UpdateAlignment("right");
    private void UpdateAlignment(string alignment) { SetCurrentLayout(CurrentLayout with { TextAlign = alignment }); ApplyAndQueueSave(); }

    private void SettingsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (!_controlsReady || _updatingControls) return;
        var layout = CurrentLayout with { FontSize = (int)Math.Round(FontSizeSlider.Value), LineHeight = Math.Round(LineHeightSlider.Value, 1) };
        if (sender == FontFamilyCombo && FontFamilyCombo.SelectedItem is FontFamily font) layout = layout with { FontFamily = font.Source };
        SetCurrentLayout(layout);
        if (ThemeCombo.SelectedItem is ComboBoxItem theme) _settings.Theme = (string)theme.Tag;
        FontSizeText.Text = layout.FontSize.ToString();
        LineHeightText.Text = layout.LineHeight.ToString("0.0");
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
        if (_workspace.ActiveTab is { } tab) tab.Layout = _settings.GetLayout();
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
        if (_readerService is not null && ReaderWebView.Visibility == Visibility.Visible) await _readerService.ApplySettingsAsync(EffectiveSettings);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SetControlsFromSettings()
    {
        _updatingControls = true;
        var layout = CurrentLayout;
        FontFamilyCombo.SelectedItem = ((IEnumerable<FontFamily>)FontFamilyCombo.ItemsSource).FirstOrDefault(font => font.Source.Equals(layout.FontFamily, StringComparison.OrdinalIgnoreCase));
        FontSizeSlider.Value = layout.FontSize;
        LineHeightSlider.Value = layout.LineHeight;
        SetThemeSelection();
        FontSizeText.Text = layout.FontSize.ToString();
        LineHeightText.Text = layout.LineHeight.ToString("0.0");
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
        MenuSetDefaultLayoutItem.Header = _localization.Get("SetDefaultLayout");
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
        TabScrollLeftButton.ToolTip = _localization.Get("ScrollTabsLeft");
        TabScrollRightButton.ToolTip = _localization.Get("ScrollTabsRight");
        TabListButton.ToolTip = _localization.Get("AllTabs");
        UpdateStatus();
        RebuildTabStrip();
    }

    private static readonly Brush ActiveTabAccent = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x36, 0x5B)));
    private static readonly Brush TabDivider = Freeze(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)));
    private static readonly Brush TabHover = Freeze(new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)));

    private static Brush Freeze(Brush brush) { brush.Freeze(); return brush; }

    private void RebuildTabStrip()
    {
        TabPanel.Children.Clear();
        foreach (var tab in _workspace.Tabs) TabPanel.Children.Add(CreateTabHeader(tab));
        TabStrip.Visibility = _workspace.Tabs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            TabPanel.Children.OfType<FrameworkElement>().FirstOrDefault(header => header.Tag == _workspace.ActiveTab)?.BringIntoView();
            UpdateTabScrollButtons();
        });
    }

    private FrameworkElement CreateTabHeader(DocumentTab tab)
    {
        var isActive = tab == _workspace.ActiveTab;
        var title = new TextBlock
        {
            Text = tab.Title,
            MaxWidth = 220,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal
        };
        var closeButton = new Button
        {
            Content = "✕",
            FontSize = 10,
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(8, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = _localization.Get("CloseTab")
        };
        closeButton.Click += async (_, _) => await CloseTabAsync(tab);

        var body = new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { title, closeButton } },
            Padding = new Thickness(10, 4, 4, 5),
            BorderThickness = new Thickness(0, 2, 0, 0),
            BorderBrush = isActive ? ActiveTabAccent : Brushes.Transparent,
            Background = isActive ? Brushes.White : Brushes.Transparent
        };
        var header = new Border { Child = body, BorderBrush = TabDivider, BorderThickness = new Thickness(0, 0, 1, 0), Tag = tab, ToolTip = tab.Path };
        header.MouseLeftButtonDown += async (_, _) => await ActivateTabAsync(tab);
        header.MouseDown += async (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            await CloseTabAsync(tab);
        };
        if (!isActive)
        {
            header.MouseEnter += (_, _) => body.Background = TabHover;
            header.MouseLeave += (_, _) => body.Background = Brushes.Transparent;
        }

        var closeItem = new MenuItem { Header = _localization.Get("CloseTab") };
        closeItem.Click += async (_, _) => await CloseTabAsync(tab);
        var closeOthersItem = new MenuItem { Header = _localization.Get("CloseOtherTabs"), IsEnabled = _workspace.Tabs.Count > 1 };
        closeOthersItem.Click += async (_, _) => await CloseOtherTabsAsync(tab);
        var copyPathItem = new MenuItem { Header = _localization.Get("CopyPath") };
        copyPathItem.Click += (_, _) =>
        {
            try { Clipboard.SetText(tab.Path); } catch (System.Runtime.InteropServices.ExternalException) { }
        };
        header.ContextMenu = new ContextMenu { Items = { closeItem, closeOthersItem, new Separator(), copyPathItem } };
        return header;
    }

    private void UpdateTabScrollButtons()
    {
        var overflow = TabScrollViewer.ScrollableWidth > 0;
        TabScrollLeftButton.Visibility = TabScrollRightButton.Visibility = overflow ? Visibility.Visible : Visibility.Collapsed;
        TabScrollLeftButton.IsEnabled = TabScrollViewer.HorizontalOffset > 0;
        TabScrollRightButton.IsEnabled = TabScrollViewer.HorizontalOffset < TabScrollViewer.ScrollableWidth;
    }

    private void TabScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateTabScrollButtons();

    private void TabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TabScrollViewer.ScrollToHorizontalOffset(TabScrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void TabScrollLeft_Click(object sender, RoutedEventArgs e) => TabScrollViewer.ScrollToHorizontalOffset(TabScrollViewer.HorizontalOffset - 160);
    private void TabScrollRight_Click(object sender, RoutedEventArgs e) => TabScrollViewer.ScrollToHorizontalOffset(TabScrollViewer.HorizontalOffset + 160);

    private void TabListButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = TabListButton, Placement = PlacementMode.Bottom };
        foreach (var tab in _workspace.Tabs)
        {
            // A TextBlock header keeps underscores in file names from becoming access keys.
            var item = new MenuItem { Header = new TextBlock { Text = tab.Title }, ToolTip = tab.Path, IsChecked = tab == _workspace.ActiveTab };
            item.Click += async (_, _) => await ActivateTabAsync(tab);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}
