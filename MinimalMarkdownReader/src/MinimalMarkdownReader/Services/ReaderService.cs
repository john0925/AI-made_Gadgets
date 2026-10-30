using System.Globalization;
using System.Text.Json;
using MinimalMarkdownReader.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MinimalMarkdownReader.Services;

public sealed class ReaderService
{
    private readonly WebView2 _webView;
    private ReaderSettings? _pendingSettings;
    private double _pendingScrollY;
    private bool _expectingDocumentNavigation;

    public event EventHandler<string>? NavigationFailed;

    // Raised with the local path when a file is dropped onto the page (WebView2 turns the drop into a file: navigation).
    public event EventHandler<string>? FileDropped;

    // Raised when the user clicks inside the page. WebView2's own focus events also fire on reloads, so they cannot
    // tell which pane the user chose.
    public event EventHandler? PageClicked;

    // Raised with the distance (CSS pixels) the user just scrolled the page, only while the page has sync scrolling on.
    public event EventHandler<double>? Scrolled;

    public ReaderService(WebView2 webView) => _webView = webView;

    public async Task InitializeAsync()
    {
        await _webView.EnsureCoreWebView2Async();
        var core = _webView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        core.NavigationStarting += OnNavigationStarting;
        core.FrameNavigationStarting += (_, e) => e.Cancel = true;
        core.NewWindowRequested += OnNewWindowRequested;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
        core.NavigationCompleted += OnNavigationCompleted;
        core.WebMessageReceived += (_, e) =>
        {
            var message = e.WebMessageAsJson;
            if (message == JsonSerializer.Serialize(ReaderPage.FocusMessage)) PageClicked?.Invoke(this, EventArgs.Empty);
            else if (ReaderPage.TryParseScrollDelta(message, out var delta)) Scrolled?.Invoke(this, delta);
        };
    }

    public void ShowDocument(MarkdownDocument document, string body, ReaderSettings settings, double scrollY = 0)
    {
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(ReaderPage.VirtualHost, document.DirectoryPath,
            CoreWebView2HostResourceAccessKind.DenyCors);
        _pendingSettings = settings.Clone();
        _pendingScrollY = scrollY;
        _expectingDocumentNavigation = true;
        _webView.NavigateToString(ReaderPage.Build(body));
    }

    public Task ApplySettingsAsync(ReaderSettings settings) => _webView.ExecuteScriptAsync(
        "window.reader.applySettings(" + JsonSerializer.Serialize(settings, ReaderPage.ScriptJsonOptions) + ");");

    public async Task<double> GetScrollPositionAsync()
    {
        var result = await _webView.ExecuteScriptAsync("window.scrollY");
        return double.TryParse(result, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && double.IsFinite(y) ? Math.Max(0, y) : 0;
    }

    // Scrolls by the other pane's distance; the page records its new position, so this scroll is not reported back.
    public Task FollowScrollAsync(double delta) =>
        _webView.ExecuteScriptAsync("window.reader.follow(" + delta.ToString("0.##", CultureInfo.InvariantCulture) + ");");

    private Task ScrollToAsync(double y) =>
        _webView.ExecuteScriptAsync("window.reader.jump(" + y.ToString("0.##", CultureInfo.InvariantCulture) + ");");

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (ReaderPage.IsAllowedResource(e.Request.Uri, e.ResourceContext == CoreWebView2WebResourceContext.Image)) return;
        e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
    }

    // Only the reader's own NavigateToString load may navigate the page; HTTP(S) links go to the default browser and everything else is dropped.
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_expectingDocumentNavigation && ReaderPage.IsDocumentNavigation(e.Uri))
        {
            _expectingDocumentNavigation = false;
            return;
        }
        e.Cancel = true;
        if (ExternalLinks.TryGetWebUri(e.Uri, out var uri)) ExternalLinks.Open(uri);
        else if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var fileUri) && fileUri.IsFile) FileDropped?.Invoke(this, fileUri.LocalPath);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (ExternalLinks.TryGetWebUri(e.Uri, out var uri)) ExternalLinks.Open(uri);
        e.Handled = true;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess && e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled) return;
        if (!e.IsSuccess)
        {
            _pendingSettings = null;
            NavigationFailed?.Invoke(this, LocalizationService.Instance.Format("WebViewNavigationFailed", e.WebErrorStatus));
            return;
        }
        if (_pendingSettings is null) return;
        var settings = _pendingSettings;
        var scrollY = _pendingScrollY;
        _pendingSettings = null;
        _pendingScrollY = 0;
        await ApplySettingsAsync(settings);
        if (scrollY > 0) await ScrollToAsync(scrollY);
    }
}
