using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace MinimalMarkdownReader.Services;

// Keeps one reader per user session and executable: a second launch hands its file paths to the first one over a
// named pipe that only the same user can open, then exits.
public sealed class SingleInstanceService : IDisposable
{
    private const string Header = "MinimalMarkdownReader/1";
    public const int MaxPaths = 100;
    public const int MaxPathLength = 32767;
    private const int MaxMessageChars = MaxPaths * (MaxPathLength + 1) + 64;

    private readonly string _pipeName;
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    public SingleInstanceService(string? instanceName = null)
    {
        instanceName ??= DefaultInstanceName();
        _pipeName = "MinimalMarkdownReader." + instanceName;
        _mutex = new Mutex(false, @"Local\MinimalMarkdownReader." + instanceName);
    }

    // Separate instances per user, logon session and install folder, so a debug build does not hand files to an installed copy.
    private static string DefaultInstanceName()
    {
        var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var folder = AppContext.BaseDirectory.ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user + "|" + folder)))[..16];
        return $"{Process.GetCurrentProcess().SessionId}.{hash}";
    }

    // An abandoned mutex (its owner exited or disposed it without releasing) also makes this instance the primary.
    public bool TryBecomePrimary()
    {
        try { return _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { return true; }
    }

    // Accepts connections until disposed; each valid message is passed to onPaths (on a pool thread). An empty list means
    // "just bring the window forward".
    public void Listen(Action<string[]> onPaths) => _ = Task.Run(() => ListenAsync(onPaths, _stop.Token));

    private async Task ListenAsync(Action<string[]> onPaths, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(_pipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // A pipe instance left open after stopping would accept clients that nobody reads from.
                if (server is not null) await server.DisposeAsync();
                if (token.IsCancellationRequested) return;
                await Task.Delay(500, CancellationToken.None);
                continue;
            }
            _ = Task.Run(() => ReceiveAsync(server, onPaths, token));
        }
    }

    private static async Task ReceiveAsync(NamedPipeServerStream server, Action<string[]> onPaths, CancellationToken token)
    {
        await using (server)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                using var reader = new StreamReader(server, new UTF8Encoding(false, true));
                var buffer = new char[8192];
                var text = new StringBuilder();
                int read;
                while ((read = await reader.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    if (text.Length + read > MaxMessageChars) return;
                    text.Append(buffer, 0, read);
                }
                if (TryDecode(text.ToString(), out var paths)) onPaths(paths);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or DecoderFallbackException) { }
        }
    }

    // Returns false when no primary instance took the message in time (none running, or it is not responding); the
    // caller then opens its own window.
    public bool TrySend(IReadOnlyList<string> paths, int timeoutMilliseconds = 5000)
    {
        try
        {
            using var timeout = new CancellationTokenSource(timeoutMilliseconds);
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.Connect(timeoutMilliseconds);
            // Windows only lets the primary instance take the foreground if this (foreground) process allows it.
            if (GetNamedPipeServerProcessId(client.SafePipeHandle, out var serverProcessId)) AllowSetForegroundWindow(serverProcessId);
            var bytes = new UTF8Encoding(false).GetBytes(Encode(paths));
            client.WriteAsync(bytes, timeout.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException) { return false; }
    }

    public static string Encode(IEnumerable<string> paths) => Header + "\n" + string.Join("\n", paths);

    // Accepts only messages with the expected header and at most MaxPaths lines that look like paths (no control
    // characters, bounded length); blank lines are skipped. The paths still go through DocumentService validation.
    public static bool TryDecode(string message, out string[] paths)
    {
        paths = [];
        var lines = message.Split('\n');
        if (lines[0] != Header || lines.Length - 1 > MaxPaths) return false;
        var result = new List<string>();
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0) continue;
            if (line.Length > MaxPathLength || line.Any(char.IsControl)) return false;
            result.Add(line);
        }
        paths = [.. result];
        return true;
    }

    public void Dispose()
    {
        _stop.Cancel();
        // Closing the handle is enough: ReleaseMutex would have to run on the thread that acquired it.
        _mutex.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
