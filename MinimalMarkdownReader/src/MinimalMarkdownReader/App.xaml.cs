using System.Windows;
using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader;

public partial class App : Application
{
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Resolve relative paths against this process's folder before they can be handed to another process.
        var paths = e.Args.Select(arg =>
        {
            try { return Path.GetFullPath(arg); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return arg; }
        }).ToArray();

        // If another instance is running, give it the files and exit; if it does not answer, open a window here.
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryBecomePrimary() && _singleInstance.TrySend(paths))
        {
            Shutdown();
            return;
        }

        var window = new MainWindow(paths);
        MainWindow = window;
        _singleInstance.Listen(received => Dispatcher.BeginInvoke(() => window.OpenFromAnotherInstance(received)));
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
