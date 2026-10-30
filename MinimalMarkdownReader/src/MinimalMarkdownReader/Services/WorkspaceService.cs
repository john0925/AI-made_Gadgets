using MinimalMarkdownReader.Models;

namespace MinimalMarkdownReader.Services;

// Tab bookkeeping without any UI: which documents are open and which tab each of the (at most two) panes shows.
// A tab is shown in at most one pane; pane 1 is only used while the view is split.
public sealed class WorkspaceService
{
    public const int PaneCount = 2;

    private readonly List<DocumentTab> _tabs = [];
    private readonly DocumentTab?[] _paneTabs = new DocumentTab?[PaneCount];

    public IReadOnlyList<DocumentTab> Tabs => _tabs;
    public int FocusedPane { get; private set; }

    // The tab in the focused pane: the one the toolbar edits and Ctrl+W closes.
    public DocumentTab? ActiveTab => _paneTabs[FocusedPane];

    public DocumentTab? TabInPane(int pane) => _paneTabs[pane];

    public int? PaneOf(DocumentTab tab)
    {
        var pane = Array.IndexOf(_paneTabs, tab);
        return pane < 0 ? null : pane;
    }

    // Returns the existing tab for this path (compared case-insensitively), keeping its layout, or appends a new one
    // that starts from defaultLayout; does not show it.
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

    public void Focus(int pane)
    {
        if (pane is < 0 or >= PaneCount) throw new ArgumentOutOfRangeException(nameof(pane));
        FocusedPane = pane;
    }

    // Shows the tab in the focused pane; a tab already visible in the other pane gets the focus instead.
    public void Activate(DocumentTab tab)
    {
        if (PaneOf(tab) is int pane) FocusedPane = pane;
        else Show(FocusedPane, tab);
    }

    // Puts the tab in the given pane. If it was showing in the other pane the two panes swap their tabs.
    public void Show(int pane, DocumentTab tab)
    {
        if (!_tabs.Contains(tab)) throw new ArgumentException("The tab is not open.", nameof(tab));
        if (PaneOf(tab) is int current && current != pane) _paneTabs[current] = _paneTabs[pane];
        _paneTabs[pane] = tab;
    }

    // Fills pane 1 with the next tab after the one in pane 0 (wrapping around) and focuses it, so the next tab the
    // user picks lands on the new side. Pane 1 stays empty when there is no other tab.
    public void Split()
    {
        if (_paneTabs[1] is null)
        {
            var start = _paneTabs[0] is { } first ? _tabs.IndexOf(first) : -1;
            _paneTabs[1] = Enumerable.Range(1, _tabs.Count).Select(i => _tabs[(start + i) % _tabs.Count]).FirstOrDefault(tab => tab != _paneTabs[0]);
        }
        FocusedPane = 1;
    }

    // Back to one pane, which keeps the focused pane's tab.
    public void Unsplit()
    {
        _paneTabs[0] = ActiveTab ?? _paneTabs[0] ?? _paneTabs[1] ?? _tabs.FirstOrDefault();
        _paneTabs[1] = null;
        FocusedPane = 0;
    }

    // Removes the tab. If a pane was showing it, that pane gets the right neighbour, then the left neighbour (never the
    // tab in the other pane), or is left empty; the result names that pane so the caller can redraw it.
    public int? Close(DocumentTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0) return null;
        var pane = PaneOf(tab);
        _tabs.RemoveAt(index);
        if (pane is not int shown) return null;
        var other = _paneTabs[1 - shown];
        _paneTabs[shown] = _tabs.Skip(index).Concat(_tabs.Take(index).Reverse()).FirstOrDefault(candidate => candidate != other);
        return shown;
    }

    // Keeps only the given tab; panes that showed other tabs become empty.
    public void CloseOthers(DocumentTab keep)
    {
        if (!_tabs.Contains(keep)) return;
        _tabs.RemoveAll(tab => tab != keep);
        for (var pane = 0; pane < PaneCount; pane++)
            if (_paneTabs[pane] != keep) _paneTabs[pane] = null;
    }

    // Cycles through tabs in strip order, skipping the tab shown in the other pane; offset is +1 for next and -1 for previous.
    public DocumentTab? Neighbor(int offset)
    {
        var other = _paneTabs[1 - FocusedPane];
        var candidates = _tabs.Where(tab => tab != other).ToList();
        if (candidates.Count == 0) return null;
        if (ActiveTab is null) return candidates[0];
        var index = candidates.IndexOf(ActiveTab);
        return candidates[((index + offset) % candidates.Count + candidates.Count) % candidates.Count];
    }
}
