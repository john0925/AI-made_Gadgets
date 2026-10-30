using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using MinimalMarkdownReader.Models;
using MinimalMarkdownReader.Services;
using Microsoft.Web.WebView2.Wpf;
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
    private readonly ReaderService?[] _readers = new ReaderService?[WorkspaceService.PaneCount];
    private readonly WebView2[] _webViews;
    private readonly Border[] _paneFrames;
    private readonly TextBlock[] _panePlaceholders;
    private bool _readersReady;
    private ReaderSettings _settings = new();
    private bool _updatingControls;
    private bool _controlsReady;
    private bool _isClosing;
    private readonly string? _initialDocumentPath;

    public MainWindow(string? initialDocumentPath = null)
    {
        _initialDocumentPath = initialDocumentPath;
        InitializeComponent();
        _webViews = [ReaderWebView, SecondaryWebView];
        _paneFrames = [PrimaryPaneFrame, SecondaryPaneFrame];
        _panePlaceholders = [PrimaryPanePlaceholder, SecondaryPanePlaceholder];
        _controlsReady = true;
        _saveTimer.Tick += SaveTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FontFamilyCombo.ItemsSource = Fonts.SystemFontFamilies.OrderBy(font => font.Source);
        _settings = await _settingsService.LoadAsync();
        _settings.UiLanguage = _localization.SetLanguage(_settings.UiLanguage);
        ApplyLocalizedText();
        ApplySplitLayout();
        RefreshView();
        foreach (var pane in Panes)
        {
            var reader = new ReaderService(_webViews[pane]);
            reader.NavigationFailed += (_, message) => StatusText.Text = message;
            reader.FileDropped += async (_, path) =>
            {
                FocusPane(pane);
                await OpenDocumentsAsync([path]);
            };
            reader.PageClicked += (_, _) => FocusPane(pane);
            _readers[pane] = reader;
        }
        try
        {
            await Task.WhenAll(_readers.Select(reader => reader!.InitializeAsync()));
            _readersReady = true;
        }
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
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
        // A drop on one side of a split view opens the files on that side.
        foreach (var pane in Panes)
        {
            var position = e.GetPosition(_paneFrames[pane]);
            if (_paneFrames[pane].IsVisible && position.X >= 0 && position.Y >= 0 && position.X < _paneFrames[pane].ActualWidth && position.Y < _paneFrames[pane].ActualHeight)
                FocusPane(pane);
        }
        await OpenDocumentsAsync(paths);
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

    private static readonly int[] Panes = [.. Enumerable.Range(0, WorkspaceService.PaneCount)];

    private bool IsSplit => _settings.SplitMode != "none";

    // Opens each file in a tab (an already open file reuses its tab) and shows the last one that loaded in the focused
    // pane, or reloads it where it is already visible.
    private async Task OpenDocumentsAsync(IEnumerable<string> paths)
    {
        if (!_readersReady) { StatusText.Text = _localization.Get("ReaderInitializing"); return; }
        DocumentTab? lastTab = null;
        foreach (var path in paths)
        {
            try
            {
                var document = await _documentService.LoadAsync(path);
                lastTab = _workspace.Open(document.Path, _settings.GetLayout());
            }
            catch (Exception ex) when (IsDocumentError(ex))
            {
                ShowOpenError(ex.Message);
            }
        }
        if (lastTab is not null) await UpdatePanesAsync(() => _workspace.Activate(lastTab), reloadPane: _workspace.PaneOf(lastTab));
        else RefreshView();
    }

    // A tab that is already visible only takes the focus; otherwise it replaces the document in the focused pane.
    private async Task ActivateTabAsync(DocumentTab tab)
    {
        if (_workspace.PaneOf(tab) is int pane) FocusPane(pane);
        else await UpdatePanesAsync(() => _workspace.Activate(tab));
    }

    private Task CloseTabAsync(DocumentTab tab) => UpdatePanesAsync(() => _workspace.Close(tab));

    private Task CloseOtherTabsAsync(DocumentTab keep) => UpdatePanesAsync(() =>
    {
        _workspace.CloseOthers(keep);
        _workspace.Activate(keep);
    });

    // Moves the tab to the other side of the split view (swapping with what is there) and focuses that side.
    private Task ShowInOtherPaneAsync(DocumentTab tab)
    {
        var target = 1 - (_workspace.PaneOf(tab) ?? _workspace.FocusedPane);
        return UpdatePanesAsync(() =>
        {
            _workspace.Show(target, tab);
            _workspace.Focus(target);
        });
    }

    // Applies a change to which tabs the panes show, then redraws only the panes whose tab changed (and reloadPane).
    private async Task UpdatePanesAsync(Action change, int? reloadPane = null)
    {
        await RememberScrollPositionsAsync();
        var before = Panes.Select(_workspace.TabInPane).ToArray();
        change();
        foreach (var pane in Panes)
            if (pane == reloadPane || _workspace.TabInPane(pane) != before[pane]) await RenderPaneAsync(pane);
        RefreshView();
    }

    // Reloads the pane's tab from disk each time, so switching tabs also picks up edits; a file that can no longer be
    // read closes its tab and the pane moves on to the replacement tab, or is left empty.
    private async Task RenderPaneAsync(int pane)
    {
        while (_workspace.TabInPane(pane) is { } tab)
        {
            try
            {
                var document = await _documentService.LoadAsync(tab.Path);
                _readers[pane]!.ShowDocument(document, _markdownService.ToSafeHtml(document, _documentService), _settings.WithLayout(tab.Layout), tab.ScrollY);
                _webViews[pane].Visibility = Visibility.Visible;
                return;
            }
            catch (Exception ex) when (IsDocumentError(ex))
            {
                ShowOpenError(ex.Message);
                _workspace.Close(tab);
            }
        }
        _webViews[pane].Visibility = Visibility.Collapsed;
    }

    private async Task RememberScrollPositionsAsync()
    {
        foreach (var pane in Panes)
            if (_workspace.TabInPane(pane) is { } tab && _readers[pane] is { } reader && _webViews[pane].Visibility == Visibility.Visible)
                tab.ScrollY = await reader.GetScrollPositionAsync();
    }

    private void FocusPane(int pane)
    {
        if (pane == _workspace.FocusedPane || (pane > 0 && !IsSplit)) return;
        _workspace.Focus(pane);
        RefreshView();
    }

    // Redraws everything that depends on which tabs are shown and which pane has the focus.
    private void RefreshView()
    {
        var hasTabs = _workspace.Tabs.Count > 0;
        PaneGrid.Visibility = hasTabs ? Visibility.Visible : Visibility.Collapsed;
        WelcomePanel.Visibility = hasTabs ? Visibility.Collapsed : Visibility.Visible;
        foreach (var pane in Panes)
        {
            _panePlaceholders[pane].Visibility = _webViews[pane].Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            _paneFrames[pane].BorderBrush = IsSplit && pane == _workspace.FocusedPane ? ActiveTabAccent : Brushes.Transparent;
        }
        SetControlsFromSettings();
        UpdateStatus();
        RebuildTabStrip();
    }

    private async void SplitMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string mode } || mode == _settings.SplitMode) return;
        var wasSplit = IsSplit;
        _settings.SplitMode = mode;
        ApplySplitLayout();
        if (wasSplit != IsSplit) await UpdatePanesAsync(IsSplit ? _workspace.Split : _workspace.Unsplit);
        else RefreshView();
        QueueSave();
    }

    private const double SplitterThickness = 5;

    // Lays out the pane grid as one pane, or two panes in columns (left/right) or rows (top/bottom) around the splitter.
    private void ApplySplitLayout()
    {
        var split = IsSplit;
        var vertical = _settings.SplitMode == "vertical";
        PaneGrid.ColumnDefinitions.Clear();
        PaneGrid.RowDefinitions.Clear();
        PaneSplitter.Visibility = SecondaryPaneFrame.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
        PrimaryPaneFrame.BorderThickness = SecondaryPaneFrame.BorderThickness = new Thickness(0, split ? 2 : 0, 0, 0);
        if (!split) return;
        GridLength[] sizes = [new(_settings.SplitRatio, GridUnitType.Star), new(SplitterThickness), new(1 - _settings.SplitRatio, GridUnitType.Star)];
        foreach (var size in sizes)
        {
            if (vertical) PaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = size });
            else PaneGrid.RowDefinitions.Add(new RowDefinition { Height = size });
        }
        UIElement[] cells = [PrimaryPaneFrame, PaneSplitter, SecondaryPaneFrame];
        for (var index = 0; index < cells.Length; index++)
        {
            Grid.SetColumn(cells[index], vertical ? index : 0);
            Grid.SetRow(cells[index], vertical ? 0 : index);
        }
        PaneSplitter.ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows;
        PaneSplitter.Cursor = vertical ? Cursors.SizeWE : Cursors.SizeNS;
        UpdatePaneMinimums();
    }

    // GridSplitter only honours pixel minimums, so the 15% limit is recomputed whenever the available space changes.
    private void UpdatePaneMinimums()
    {
        if (PaneGrid.ColumnDefinitions.Count == 3)
            PaneGrid.ColumnDefinitions[0].MinWidth = PaneGrid.ColumnDefinitions[2].MinWidth = Math.Max(0, (PaneGrid.ActualWidth - SplitterThickness) * ReaderSettings.MinSplitRatio);
        else if (PaneGrid.RowDefinitions.Count == 3)
            PaneGrid.RowDefinitions[0].MinHeight = PaneGrid.RowDefinitions[2].MinHeight = Math.Max(0, (PaneGrid.ActualHeight - SplitterThickness) * ReaderSettings.MinSplitRatio);
    }

    private void PaneGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePaneMinimums();

    private void PaneSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var (first, second) = PaneGrid.ColumnDefinitions.Count == 3
            ? (PaneGrid.ColumnDefinitions[0].ActualWidth, PaneGrid.ColumnDefinitions[2].ActualWidth)
            : (PaneGrid.RowDefinitions[0].ActualHeight, PaneGrid.RowDefinitions[2].ActualHeight);
        if (first + second > 0) SetSplitRatio(first / (first + second));
    }

    private void PaneSplitter_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        e.Handled = true;
        SetSplitRatio(ReaderSettings.DefaultSplitRatio);
    }

    private void SetSplitRatio(double ratio)
    {
        _settings.SplitRatio = Math.Round(Math.Clamp(ratio, ReaderSettings.MinSplitRatio, 1 - ReaderSettings.MinSplitRatio), 3);
        ApplySplitLayout();
        QueueSave();
    }

    private void PaneFrame_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => FocusPane(Array.IndexOf(_paneFrames, sender));

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
        _settings = new ReaderSettings { UiLanguage = _localization.CurrentLanguage, SplitMode = _settings.SplitMode, SplitRatio = _settings.SplitRatio };
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

    // Pushes the global settings plus each visible tab's own layout to its pane, then schedules a save.
    private async void ApplyAndQueueSave()
    {
        foreach (var pane in Panes)
            if (_workspace.TabInPane(pane) is { } tab && _readers[pane] is { } reader && _webViews[pane].Visibility == Visibility.Visible)
                await reader.ApplySettingsAsync(_settings.WithLayout(tab.Layout));
        QueueSave();
    }

    private void QueueSave()
    {
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
        foreach (var button in new[] { SplitNoneButton, SplitVerticalButton, SplitHorizontalButton })
        {
            if ((string)button.Tag == _settings.SplitMode) button.Background = SelectedButtonBackground;
            else button.ClearValue(BackgroundProperty);
        }
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
        SplitLabel.Text = _localization.Get("SplitView");
        SplitNoneButton.ToolTip = _localization.Get("SplitNone");
        SplitVerticalButton.ToolTip = _localization.Get("SplitVertical");
        SplitHorizontalButton.ToolTip = _localization.Get("SplitHorizontal");
        PaneSplitter.ToolTip = _localization.Get("SplitterHint");
        PrimaryPanePlaceholder.Text = SecondaryPanePlaceholder.Text = _localization.Get("PanePlaceholder");
        UpdateStatus();
        RebuildTabStrip();
    }

    private static readonly Brush ActiveTabAccent = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x36, 0x5B)));
    private static readonly Brush TabDivider = Freeze(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)));
    private static readonly Brush TabHover = Freeze(new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)));
    private static readonly Brush SelectedButtonBackground = Freeze(new SolidColorBrush(Color.FromRgb(0xCF, 0xDD, 0xF0)));

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
        var pane = _workspace.PaneOf(tab);
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

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        // In a split view a small badge tells which side each visible tab is on.
        if (IsSplit && pane is int side) content.Children.Add(new Border
        {
            Child = new TextBlock { Text = PaneName(side), FontSize = 10, Foreground = Brushes.White },
            Background = isActive ? ActiveTabAccent : Brushes.Gray,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 0, 4, 1),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(title);
        content.Children.Add(closeButton);
        var body = new Border
        {
            Child = content,
            Padding = new Thickness(10, 4, 4, 5),
            BorderThickness = new Thickness(0, 2, 0, 0),
            BorderBrush = isActive ? ActiveTabAccent : Brushes.Transparent,
            Background = pane is not null ? Brushes.White : Brushes.Transparent
        };
        var header = new Border { Child = body, BorderBrush = TabDivider, BorderThickness = new Thickness(0, 0, 1, 0), Tag = tab, ToolTip = tab.Path };
        header.MouseLeftButtonDown += async (_, _) => await ActivateTabAsync(tab);
        header.MouseDown += async (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle) return;
            e.Handled = true;
            await CloseTabAsync(tab);
        };
        if (pane is null)
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
        var otherPaneItem = new MenuItem { Header = _localization.Get("ShowInOtherPane"), IsEnabled = IsSplit };
        otherPaneItem.Click += async (_, _) => await ShowInOtherPaneAsync(tab);
        header.ContextMenu = new ContextMenu { Items = { otherPaneItem, new Separator(), closeItem, closeOthersItem, new Separator(), copyPathItem } };
        return header;
    }

    private string PaneName(int pane) => _localization.Get(_settings.SplitMode == "vertical"
        ? (pane == 0 ? "PaneLeft" : "PaneRight")
        : (pane == 0 ? "PaneTop" : "PaneBottom"));

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
