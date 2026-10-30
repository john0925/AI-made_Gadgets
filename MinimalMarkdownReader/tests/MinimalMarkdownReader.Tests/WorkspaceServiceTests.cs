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
        var next = workspace.Close(workspace.Tabs[1]);
        Assert.Equal("c.md", next!.Title);
        Assert.Null(workspace.ActiveTab);
    }

    [Fact]
    public void Close_LastActiveTab_FallsBackToLeftNeighbour()
    {
        var workspace = WithTabs("a.md", "b.md");
        workspace.Activate(workspace.Tabs[1]);
        Assert.Equal("a.md", workspace.Close(workspace.Tabs[1])!.Title);
    }

    [Fact]
    public void Close_OnlyTab_ReturnsNull()
    {
        var workspace = WithTabs("a.md");
        workspace.Activate(workspace.Tabs[0]);
        Assert.Null(workspace.Close(workspace.Tabs[0]));
        Assert.Empty(workspace.Tabs);
    }

    [Fact]
    public void Close_InactiveTab_KeepsActiveTab()
    {
        var workspace = WithTabs("a.md", "b.md");
        var active = workspace.Tabs[0];
        workspace.Activate(active);
        Assert.Same(active, workspace.Close(workspace.Tabs[1]));
        Assert.Same(active, workspace.ActiveTab);
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
