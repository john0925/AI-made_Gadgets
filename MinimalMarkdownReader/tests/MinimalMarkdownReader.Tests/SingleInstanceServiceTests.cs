using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader.Tests;

public sealed class SingleInstanceServiceTests
{
    private static string UniqueName() => "test." + Guid.NewGuid().ToString("N");

    [Fact]
    public void EncodeThenDecode_RoundTripsPaths()
    {
        string[] paths = [@"C:\docs\a.md", @"D:\文件\筆記 1.markdown"];
        Assert.True(SingleInstanceService.TryDecode(SingleInstanceService.Encode(paths), out var decoded));
        Assert.Equal(paths, decoded);
    }

    [Fact]
    public void Decode_EmptyListMeansActivateOnly()
    {
        Assert.True(SingleInstanceService.TryDecode(SingleInstanceService.Encode([]), out var decoded));
        Assert.Empty(decoded);
    }

    [Theory]
    [InlineData("C:\\a.md")]
    [InlineData("OtherApp/1\nC:\\a.md")]
    [InlineData("MinimalMarkdownReader/1\nC:\\a\u0007.md")]
    [InlineData("MinimalMarkdownReader/1\nC:\\a.md\r")]
    public void Decode_RejectsWrongHeaderOrControlCharacters(string message)
    {
        Assert.False(SingleInstanceService.TryDecode(message, out _));
    }

    [Fact]
    public void Decode_RejectsTooManyOrTooLongPaths()
    {
        var tooMany = Enumerable.Range(0, SingleInstanceService.MaxPaths + 1).Select(i => $@"C:\{i}.md");
        Assert.False(SingleInstanceService.TryDecode(SingleInstanceService.Encode(tooMany), out _));
        Assert.False(SingleInstanceService.TryDecode(SingleInstanceService.Encode([new string('a', SingleInstanceService.MaxPathLength + 1)]), out _));
    }

    [Fact]
    public void SecondInstance_IsNotPrimary_UntilFirstIsDisposed()
    {
        // Synchronous on purpose: a mutex belongs to the thread that acquired it, which must stay alive during the test.
        var name = UniqueName();
        var first = new SingleInstanceService(name);
        Assert.True(first.TryBecomePrimary());
        Assert.False(TryOnOtherThread(name));
        first.Dispose();
        Assert.True(TryOnOtherThread(name));
    }

    private static bool TryOnOtherThread(string name)
    {
        var result = false;
        var thread = new Thread(() =>
        {
            using var instance = new SingleInstanceService(name);
            result = instance.TryBecomePrimary();
        });
        thread.Start();
        thread.Join();
        return result;
    }

    [Fact]
    public async Task TrySend_DeliversPathsToListeningPrimary()
    {
        var name = UniqueName();
        using var primary = new SingleInstanceService(name);
        primary.TryBecomePrimary();
        var received = new TaskCompletionSource<string[]>();
        primary.Listen(paths => received.TrySetResult(paths));

        using var secondary = new SingleInstanceService(name);
        Assert.True(await Task.Run(() => secondary.TrySend([@"C:\docs\a.md", @"C:\docs\b.md"])));
        var paths = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([@"C:\docs\a.md", @"C:\docs\b.md"], paths);
    }

    [Fact]
    public async Task TrySend_AfterPrimaryStops_ReturnsFalseInsteadOfHanging()
    {
        var name = UniqueName();
        var primary = new SingleInstanceService(name);
        primary.TryBecomePrimary();
        primary.Listen(_ => { });
        await Task.Delay(200);
        primary.Dispose();
        await Task.Delay(200);
        using var secondary = new SingleInstanceService(name);
        var send = Task.Run(() => secondary.TrySend([@"C:.md"], timeoutMilliseconds: 300));
        Assert.False(await send.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void TrySend_WithoutPrimary_ReturnsFalse()
    {
        using var lonely = new SingleInstanceService(UniqueName());
        Assert.False(lonely.TrySend([@"C:\a.md"], timeoutMilliseconds: 200));
    }
}
