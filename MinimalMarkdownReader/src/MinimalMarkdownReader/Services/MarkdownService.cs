using System.Net;
using System.Text.RegularExpressions;
using Markdig;

namespace MinimalMarkdownReader.Services;

public sealed class MarkdownService
{
    private static readonly Regex ImagePattern = new("<img(?<before>[^>]*?)src=\"(?<src>[^\"]*)\"(?<after>[^>]*)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LinkPattern = new("<a(?<before>[^>]*?)href=\"(?<href>[^\"]*)\"(?<after>[^>]*)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // UseAdvancedExtensions() minus GenericAttributes (arbitrary attributes such as onload/style) and MediaLinks (remote iframe/video).
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAbbreviations()
        .UseAutoIdentifiers()
        .UseCitations()
        .UseCustomContainers()
        .UseDefinitionLists()
        .UseEmphasisExtras()
        .UseFigures()
        .UseFooters()
        .UseFootnotes()
        .UseGridTables()
        .UseMathematics()
        .UsePipeTables()
        .UseListExtras()
        .UseTaskLists()
        .UseDiagrams()
        .UseAutoLinks()
        .DisableHtml()
        .Build();

    public string ToSafeHtml(MarkdownDocument document, DocumentService documentService)
    {
        var html = Markdown.ToHtml(document.Content, _pipeline);
        html = ImagePattern.Replace(html, match =>
        {
            var source = WebUtility.HtmlDecode(match.Groups["src"].Value);
            if (!documentService.TryResolveRelativeImage(document, source, out var imagePath)) return $"<span class=\"blocked-resource\">{WebUtility.HtmlEncode(LocalizationService.Instance.Get("BlockedImage"))}</span>";
            var safePath = string.Join('/', imagePath.Split('/').Select(Uri.EscapeDataString));
            return $"<img{match.Groups["before"].Value}src=\"https://{ReaderPage.VirtualHost}/{safePath}\"{match.Groups["after"].Value}>";
        });
        return LinkPattern.Replace(html, match =>
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            return Uri.TryCreate(href, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? $"<a{match.Groups["before"].Value}href=\"{WebUtility.HtmlEncode(uri.AbsoluteUri)}\"{match.Groups["after"].Value}>"
                : "<a>";
        });
    }
}
