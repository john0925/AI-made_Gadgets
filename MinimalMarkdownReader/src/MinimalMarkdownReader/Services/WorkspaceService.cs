using MinimalMarkdownReader.Models;

namespace MinimalMarkdownReader.Services;

// Tab bookkeeping without any UI: which documents are open and which one is shown.
public sealed class WorkspaceService
{
    private readonly List<DocumentTab> _tabs = [];

    public IReadOnlyList<DocumentTab> Tabs => _tabs;
    public DocumentTab? ActiveTab { get; private set; }

    // Returns the existing tab for this path (compared case-insensitively), keeping its layout, or appends a new one
    // that starts from defaultLayout; does not activate it.
    public DocumentTab Open(string fullPath, LayoutSettings defaultLayout)
    {
        var existing = Find(fullPath);
        if (existing is not null) return existing;
        var tab = new DocumentTab(fullPath, defaultLayout);
        _tabs.Add(tab);
        return tab;
    }

    public DocumentTab? Find(string fullPath) =>
        _tabs.FirstOrDefault(tab => tab.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));

    public void Activate(DocumentTab tab)
    {
        if (!_tabs.Contains(tab)) throw new ArgumentException("The tab is not open.", nameof(tab));
        ActiveTab = tab;
    }

    // Removes the tab and returns the tab that should be shown next: the current active tab if another tab was
    // closed, otherwise the right neighbour, then the left neighbour, or null when no tabs remain.
    public DocumentTab? Close(DocumentTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0) return ActiveTab;
        _tabs.RemoveAt(index);
        if (ActiveTab != tab) return ActiveTab;
        ActiveTab = null;
        return _tabs.Count == 0 ? null : _tabs[Math.Min(index, _tabs.Count - 1)];
    }

    public void CloseOthers(DocumentTab keep)
    {
        if (!_tabs.Contains(keep)) return;
        _tabs.RemoveAll(tab => tab != keep);
        if (ActiveTab != keep) ActiveTab = null;
    }

    // Cycles through tabs in strip order; offset is +1 for next and -1 for previous.
    public DocumentTab? Neighbor(int offset)
    {
        if (_tabs.Count == 0) return null;
        if (ActiveTab is null) return _tabs[0];
        var index = _tabs.IndexOf(ActiveTab);
        return _tabs[((index + offset) % _tabs.Count + _tabs.Count) % _tabs.Count];
    }
}
