using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MinimalMarkdownReader.Models;

namespace MinimalMarkdownReader.Services;

public static class ReaderPage
{
    public const string VirtualHost = "markdown.localhost";

    // The only message the page sends to the host: the user pressed a mouse button inside the page.
    public const string FocusMessage = "focus";

    public static JsonSerializerOptions ScriptJsonOptions { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly string Script = "const palettes=" + JsonSerializer.Serialize(ThemePalettes.BuiltIn, ScriptJsonOptions) + ";" + """
window.reader={applySettings:s=>{const t=palettes[s.theme]||s.customColors;const r=document.documentElement;r.style.setProperty('--bg',t.background);r.style.setProperty('--text',t.text);r.style.setProperty('--heading',t.heading);r.style.setProperty('--link',t.link);r.style.setProperty('--code',t.codeBackground);r.style.setProperty('--font',JSON.stringify(s.fontFamily));r.style.setProperty('--size',s.fontSize+'px');r.style.setProperty('--line',s.lineHeight);r.style.setProperty('--align',s.textAlign);r.dataset.align=s.textAlign}};
""" + "addEventListener('pointerdown',()=>chrome.webview.postMessage('" + FocusMessage + "'),true);";

    // Only the page's own script (allowed by hash) may run; event-handler attributes, remote content, frames and media are blocked.
    public static string ContentSecurityPolicy { get; } =
        $"default-src 'none'; img-src https://{VirtualHost}; style-src 'unsafe-inline'; " +
        $"script-src 'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Script)))}'; base-uri 'none'; form-action 'none'";

    private static readonly HashSet<string> NetworkSchemes = new(StringComparer.OrdinalIgnoreCase) { "http", "https", "ws", "wss", "ftp", "file" };

    // Network and file requests are limited to images served from the document folder; other schemes (the page itself) are governed by the CSP.
    public static bool IsAllowedResource(string uri, bool isImage)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return false;
        if (!NetworkSchemes.Contains(parsed.Scheme)) return true;
        return isImage && parsed.Scheme == Uri.UriSchemeHttps && parsed.Host.Equals(VirtualHost, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDocumentNavigation(string uri) =>
        uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase);

    private const string Css = """
:root{--font:'Microsoft JhengHei';--size:18px;--line:1.8;--align:left}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:var(--font),sans-serif;font-size:var(--size);line-height:var(--line)}main{width:95%;max-width:none;margin:0 auto;padding:40px 0 80px}main{text-align:var(--align)}th{text-align:inherit}h1,h2,h3,h4,h5,h6{color:var(--heading)}a{color:var(--link)}blockquote{border-left:4px solid var(--link);margin-left:0;padding-left:1em}pre{background:var(--code);padding:1em;overflow-x:auto;text-align:left}pre,code{font-family:Consolas,monospace}table{border-collapse:collapse;max-width:100%}th,td{border:1px solid color-mix(in srgb,var(--text) 35%,transparent);padding:.45em}img{max-width:100%;height:auto}[data-align=center] :is(ul,ol),[data-align=right] :is(ul,ol){list-style-position:inside;padding-left:0}[data-align=right] :is(ul,ol) :is(ul,ol){padding-right:1.5em}[data-align=center] table{margin-left:auto;margin-right:auto}[data-align=right] table{margin-left:auto}[data-align=center] blockquote{display:table;margin-left:auto;margin-right:auto}[data-align=right] blockquote{border-left:0;border-right:4px solid var(--link);margin-right:0;padding-left:0;padding-right:1em}[data-align=right] dd{margin-left:0;margin-right:2em}.blocked-resource{color:#a33;font-style:italic}@media(max-width:600px){main{width:92%;padding:24px 0}}
""";

    public static string Build(string body)
    {
        var soft = ThemePalettes.Soft;
        return "<!doctype html><html><head><meta charset=\"utf-8\">"
            + $"<meta http-equiv=\"Content-Security-Policy\" content=\"{ContentSecurityPolicy}\">"
            + $"<style>:root{{--bg:{soft.Background};--text:{soft.Text};--heading:{soft.Heading};--link:{soft.Link};--code:{soft.CodeBackground}}}"
            + Css + "</style></head><body><main>" + body + "</main><script>" + Script + "</script></body></html>";
    }
}
