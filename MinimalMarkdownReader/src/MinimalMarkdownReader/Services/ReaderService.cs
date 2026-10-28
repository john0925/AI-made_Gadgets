using System.Diagnostics;
using System.Text.Json;
using MinimalMarkdownReader.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MinimalMarkdownReader.Services;

public sealed class ReaderService
{
    private readonly WebView2 _webView;
    private ReaderSettings? _pendingSettings;
    private bool _expectingDocumentNavigation;

    public event EventHandler<string>? NavigationFailed;

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
    }

    public void ShowDocument(MarkdownDocument document, string body, ReaderSettings settings)
    {
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(ReaderPage.VirtualHost, document.DirectoryPath,
            CoreWebView2HostResourceAccessKind.DenyCors);
        _pendingSettings = settings.Clone();
        _expectingDocumentNavigation = true;
        _webView.NavigateToString(ReaderPage.Build(body));
    }

    public Task ApplySettingsAsync(ReaderSettings settings) => _webView.ExecuteScriptAsync(
        "window.reader.applySettings(" + JsonSerializer.Serialize(settings, ReaderPage.ScriptJsonOptions) + ");");

    private static void OpenExternal(Uri uri)
    {
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
    }

    private static bool IsWebUri(string value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

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
        if (IsWebUri(e.Uri, out var uri)) OpenExternal(uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (IsWebUri(e.Uri, out var uri)) OpenExternal(uri);
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
        _pendingSettings = null;
        await ApplySettingsAsync(settings);
    }
}
