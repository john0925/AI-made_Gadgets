using System.Windows;

namespace MinimalMarkdownReader;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var initialDocumentPath = e.Args.Length > 0 ? e.Args[0] : null;
        var window = new MainWindow(initialDocumentPath);
        MainWindow = window;
        window.Show();
    }
}
