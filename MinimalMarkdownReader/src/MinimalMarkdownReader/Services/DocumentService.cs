using System.Text;

namespace MinimalMarkdownReader.Services;

public sealed record MarkdownDocument(string Path, string DirectoryPath, string Content);

public sealed class DocumentService
{
    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };

    public async Task<MarkdownDocument> LoadAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !MarkdownExtensions.Contains(Path.GetExtension(path)))
            throw new ArgumentException(LocalizationService.Instance.Get("InvalidFileType"), nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException(LocalizationService.Instance.Get("FileNotFound"), fullPath);
        string content;
        try { content = await File.ReadAllTextAsync(fullPath, new UTF8Encoding(false, true)); }
        catch (DecoderFallbackException ex) { throw new InvalidDataException(LocalizationService.Instance.Get("InvalidEncoding"), ex); }
        return new MarkdownDocument(fullPath, Path.GetDirectoryName(fullPath)!, content);
    }

    public bool TryResolveRelativeImage(MarkdownDocument document, string source, out string mappedPath)
    {
        mappedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(source) || Uri.TryCreate(source, UriKind.Absolute, out _) || Path.IsPathRooted(source)) return false;
        var candidate = Path.GetFullPath(Path.Combine(document.DirectoryPath, source.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(document.DirectoryPath)) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !ImageExtensions.Contains(Path.GetExtension(candidate)) || !File.Exists(candidate)) return false;
        mappedPath = candidate[root.Length..].Replace(Path.DirectorySeparatorChar, '/');
        return true;
    }
}
