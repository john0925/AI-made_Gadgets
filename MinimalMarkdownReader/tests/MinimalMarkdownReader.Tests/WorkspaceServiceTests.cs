using MinimalMarkdownReader.Models;
using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class WorkspaceServiceTests
{
    private static readonly LayoutSettings Default = new ReaderSettings().GetLayout();

    private static WorkspaceService WithTabs(params string[] names)
    {
        var workspace = new WorkspaceService();
        foreach (var name in names) workspace.Open(Path.Combine(@"C:\docs", name), Default);
        return workspace;
    }

    [Fact]
    public void Open_SamePathTwice_ReusesTabIgnoringCase()
    {
        var workspace = new WorkspaceService();
        var first = workspace.Open(@"C:\docs\Note.md", Default);
        var second = workspace.Open(@"c:\DOCS\note.md", Default);
        Assert.Same(first, second);
        Assert.Single(workspace.Tabs);
    }

    [Fact]
    public void Open_DoesNotActivate()
    {
        var workspace = WithTabs("a.md");
        Assert.Null(workspace.ActiveTab);
        Assert.Equal("a.md", workspace.Tabs[0].Title);
    }

    [Fact]
    public void Close_ActiveTab_PrefersRightNeighbour()
    {
        var workspace = WithTabs("a.md", "b.md", "c.md");
        workspace.Activate(workspace.Tabs[1]);
        Assert.Equal(0, workspace.Close(workspace.Tabs[1]));
        Assert.Equal("c.md", workspace.ActiveTab!.Title);
    }

    [Fact]
    public void Close_LastActiveTab_FallsBackToLeftNeighbour()
    {
        var workspace = WithTabs("a.md", "b.md");
        workspace.Activate(workspace.Tabs[1]);
        workspace.Close(workspace.Tabs[1]);
        Assert.Equal("a.md", workspace.ActiveTab!.Title);
    }

    [Fact]
    public void Close_OnlyTab_LeavesPaneEmpty()
    {
        var workspace = WithTabs("a.md");
        workspace.Activate(workspace.Tabs[0]);
        Assert.Equal(0, workspace.Close(workspace.Tabs[0]));
        Assert.Null(workspace.ActiveTab);
        Assert.Empty(workspace.Tabs);
    }

    [Fact]
    public void Close_HiddenTab_ChangesNoPane()
    {
        var workspace = WithTabs("a.md", "b.md");
        var active = workspace.Tabs[0];
        workspace.Activate(active);
        Assert.Null(workspace.Close(workspace.Tabs[1]));
        Assert.Same(active, workspace.ActiveTab);
    }

    [Fact]
    public void Close_InSplitView_NeverPicksTabFromOtherPane()
    {
        var workspace = WithTabs("a.md", "b.md", "c.md");
        workspace.Activate(workspace.Tabs[1]);
        workspace.Split();
        Assert.Equal("c.md", workspace.TabInPane(1)!.Title);
        Assert.Equal(0, workspace.Close(workspace.Tabs[1]));
        Assert.Equal("a.md", workspace.TabInPane(0)!.Title);
        Assert.Equal(0, workspace.Close(workspace.Tabs[0]));
        Assert.Null(workspace.TabInPane(0));
        Assert.Equal("c.md", workspace.TabInPane(1)!.Title);
    }

    [Fact]
    public void Split_ShowsNextTabInSecondPaneAndFocusesIt()
    {
        var workspace = WithTabs("a.md", "b.md", "c.md");
        workspace.Activate(workspace.Tabs[2]);
        workspace.Split();
        Assert.Equal(1, workspace.FocusedPane);
        Assert.Equal(("c.md", "a.md"), (workspace.TabInPane(0)!.Title, workspace.TabInPane(1)!.Title));
    }

    [Fact]
    public void Split_WithOneTab_LeavesSecondPaneEmpty()
    {
        var workspace = WithTabs("a.md");
        workspace.Activate(workspace.Tabs[0]);
        workspace.Split();
        Assert.Null(workspace.ActiveTab);
        workspace.Activate(workspace.Tabs[0]);
        Assert.Equal(0, workspace.FocusedPane);
        Assert.Null(workspace.TabInPane(1));
    }

    [Fact]
    public void Activate_InSplitView_ReplacesFocusedPaneOnly()
    {
        var workspace = WithTabs("a.md", "b.md", "c.md");
        workspace.Activate(workspace.Tabs[0]);
        workspace.Split();
        workspace.Activate(workspace.Tabs[2]);
        Assert.Equal(("a.md", "c.md"), (workspace.TabInPane(0)!.Title, workspace.TabInPane(1)!.Title));
        Assert.Equal(1, workspace.PaneOf(workspace.Tabs[2]));
        Assert.Null(workspace.PaneOf(workspace.Tabs[1]));
    }

    [Fact]
    public void Show_TabFromOtherPane_SwapsPanes()
    {
        var workspace = WithTabs("a.md", "b.md");
        workspace.Activate(workspace.Tabs[0]);
        workspace.Split();
        workspace.Show(1, workspace.Tabs[0]);
        Assert.Equal(("b.md", "a.md"), (workspace.TabInPane(0)!.Title, workspace.TabInPane(1)!.Title));
    }

    [Fact]
    public void Unsplit_KeepsFocusedPaneTab()
    {
        var workspace = WithTabs("a.md", "b.md");
        workspace.Activate(workspace.Tabs[0]);
        workspace.Split();
        workspace.Unsplit();
        Assert.Equal(0, workspace.FocusedPane);
        Assert.Equal("b.md", workspace.ActiveTab!.Title);
        Assert.Null(workspace.TabInPane(1));
    }

    [Fact]
    public void Neighbor_InSplitView_SkipsOtherPaneTab()
    {
        var workspace = WithTabs("a.md", "b.md", "c.md");
        workspace.Activate(workspace.Tabs[0]);
        workspace.Split();
        workspace.Focus(0);
        Assert.Equal("c.md", workspace.Neighbor(1)!.Title);
    }

    [Fact]
    public void CloseOthers_KeepsOnlyChosenTab()
    {
        var workspace = WithTabs("a.md", "b.md", "c.md");
        var keep = workspace.Tabs[1];
        workspace.Activate(keep);
        workspace.CloseOthers(keep);
        Assert.Same(keep, Assert.Single(workspace.Tabs));
        Assert.Same(keep, workspace.ActiveTab);
    }

    [Fact]
    public void CloseOthers_EmptiesPaneShowingRemovedTab()
    {
        var workspace = WithTabs("a.md", "b.md");
        workspace.Activate(workspace.Tabs[0]);
        workspace.Split();
        workspace.CloseOthers(workspace.Tabs[0]);
        Assert.Null(workspace.TabInPane(1));
        Assert.Equal("a.md", workspace.TabInPane(0)!.Title);
    }

    [Theory]
    [InlineData(1, "a.md")]
    [InlineData(-1, "b.md")]
    public void Neighbor_WrapsAround(int offset, string expected)
    {
        var workspace = WithTabs("a.md", "b.md", "c.md");
        workspace.Activate(workspace.Tabs[2]);
        Assert.Equal(expected, workspace.Neighbor(offset)!.Title);
    }

    [Fact]
    public void Activate_UnknownTab_Throws()
    {
        var workspace = WithTabs("a.md");
        Assert.Throws<ArgumentException>(() => workspace.Activate(new DocumentTab(@"C:\other.md", Default)));
    }

    [Fact]
    public void Open_NewTabStartsFromDefaultLayout_ReopenKeepsTabLayout()
    {
        var workspace = new WorkspaceService();
        var tab = workspace.Open(@"C:\docs\a.md", Default);
        Assert.Equal(Default, tab.Layout);
        tab.Layout = tab.Layout with { TextAlign = "right", FontSize = 24 };
        var reopened = workspace.Open(@"C:\docs\a.md", Default with { TextAlign = "center" });
        Assert.Same(tab, reopened);
        Assert.Equal("right", reopened.Layout.TextAlign);
        Assert.Equal(24, reopened.Layout.FontSize);
    }

    [Fact]
    public void TabLayouts_AreIndependent()
    {
        var workspace = WithTabs("a.md", "b.md");
        workspace.Tabs[0].Layout = workspace.Tabs[0].Layout with { TextAlign = "center" };
        Assert.Equal("center", workspace.Tabs[0].Layout.TextAlign);
        Assert.Equal(Default.TextAlign, workspace.Tabs[1].Layout.TextAlign);
    }

    [Fact]
    public void WithLayout_OverridesOnlyLayoutAndLeavesOriginalUntouched()
    {
        var global = new ReaderSettings { Theme = "dark", UiLanguage = "en" };
        var rendered = global.WithLayout(new LayoutSettings("right", "Consolas", 30, 2.2));
        Assert.Equal(("right", "Consolas", 30, 2.2), (rendered.TextAlign, rendered.FontFamily, rendered.FontSize, rendered.LineHeight));
        Assert.Equal(("dark", "en"), (rendered.Theme, rendered.UiLanguage));
        Assert.Equal(new ReaderSettings().GetLayout(), global.GetLayout());
    }
}
