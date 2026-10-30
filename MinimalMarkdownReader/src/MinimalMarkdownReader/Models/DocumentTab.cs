namespace MinimalMarkdownReader.Models;

public sealed class DocumentTab
{
    public DocumentTab(string path, LayoutSettings layout)
    {
        Path = path;
        Layout = layout;
    }

    public string Path { get; }
    public string Title => System.IO.Path.GetFileName(Path);

    // Alignment, font, size and line height for this tab only; not persisted after the tab closes.
    public LayoutSettings Layout { get; set; }

    // Vertical scroll offset (CSS pixels) restored when the tab is shown again.
    public double ScrollY { get; set; }
}
