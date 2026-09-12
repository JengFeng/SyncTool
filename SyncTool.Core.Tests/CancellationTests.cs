using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class CancellationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Cancelled_run_does_not_write_state()
    {
        var a = Create("A"); var b = Create("B"); var state = Path.Combine(_root, "state.json");
        await File.WriteAllTextAsync(Path.Combine(a, "data.txt"), "data");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b, StateFilePath = state }).RunAsync(false, cancellationToken: cancellation.Token));
        Assert.False(File.Exists(state));
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
