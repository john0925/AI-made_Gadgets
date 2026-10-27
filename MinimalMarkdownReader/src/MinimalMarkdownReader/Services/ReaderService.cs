using System.Diagnostics;
using System.Text.Json;
using MinimalMarkdownReader.Models;
using Microsoft.Web.WebView2.Wpf;

namespace MinimalMarkdownReader.Services;

public sealed class ReaderService
{
    private readonly WebView2 _webView;
    private ReaderSettings? _pendingSettings;

    public event EventHandler<string>? NavigationFailed;

    public ReaderService(WebView2 webView) => _webView = webView;

    public async Task InitializeAsync()
    {
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
        _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
    }

    public void ShowDocument(MarkdownDocument document, string body, ReaderSettings settings)
    {
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("markdown.localhost", document.DirectoryPath,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.DenyCors);
        _pendingSettings = settings.Clone();
        _webView.NavigateToString(HtmlShell(body));
    }

    private static readonly JsonSerializerOptions ScriptJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public Task ApplySettingsAsync(ReaderSettings settings) => _webView.ExecuteScriptAsync(
        "window.reader.applySettings(" + JsonSerializer.Serialize(settings, ScriptJsonOptions) + ");");

    private static string HtmlShell(string body)
    {
        var soft = ThemePalettes.Soft;
        return """
<!doctype html><html><head><meta charset="utf-8"><style>
""" + $":root{{--bg:{soft.Background};--text:{soft.Text};--heading:{soft.Heading};--link:{soft.Link};--code:{soft.CodeBackground}}}" + """
:root{--font:'Microsoft JhengHei';--size:18px;--line:1.8;--align:left}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:var(--font),sans-serif;font-size:var(--size);line-height:var(--line)}main{width:95%;max-width:none;margin:0 auto;padding:40px 0 80px}h1,h2,h3,h4,h5,h6,p,blockquote{text-align:var(--align)}h1,h2,h3,h4,h5,h6{color:var(--heading)}a{color:var(--link)}blockquote{border-left:4px solid var(--link);margin-left:0;padding-left:1em}pre{background:var(--code);padding:1em;overflow-x:auto;text-align:left}pre,code{font-family:Consolas,monospace}table{border-collapse:collapse;max-width:100%}th,td{border:1px solid color-mix(in srgb,var(--text) 35%,transparent);padding:.45em}img{max-width:100%;height:auto}.blocked-resource{color:#a33;font-style:italic}@media(max-width:600px){main{width:92%;padding:24px 0}}
</style></head><body><main>
""" + body + """
</main><script>
""" + "const palettes=" + JsonSerializer.Serialize(ThemePalettes.BuiltIn, ScriptJsonOptions) + ";" + """
window.reader={applySettings:s=>{const t=palettes[s.theme]||s.customColors;const r=document.documentElement;r.style.setProperty('--bg',t.background);r.style.setProperty('--text',t.text);r.style.setProperty('--heading',t.heading);r.style.setProperty('--link',t.link);r.style.setProperty('--code',t.codeBackground);r.style.setProperty('--font',JSON.stringify(s.fontFamily));r.style.setProperty('--size',s.fontSize+'px');r.style.setProperty('--line',s.lineHeight);r.style.setProperty('--align',s.textAlign)}};
</script></body></html>
""";
    }

    private static void OpenExternal(Uri uri)
    {
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
    }
    private void OnNavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) { e.Cancel = true; OpenExternal(uri); }
    }
    private void OnNewWindowRequested(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) OpenExternal(uri);
        e.Handled = true;
    }

    private async void OnNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
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
