using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SyncProgressTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsync_reports_scanning_and_per_item_progress()
    {
        var a = Path.Combine(_root, "A"); var b = Path.Combine(_root, "B");
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        await File.WriteAllTextAsync(Path.Combine(a, "one.txt"), "1");
        await File.WriteAllTextAsync(Path.Combine(a, "two.txt"), "2");
        var captured = new List<SyncProgress>();

        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b })
            .RunAsync(dryRun: true, progress: new CaptureProgress(captured));

        Assert.True(result.Success);
        Assert.Contains(captured, p => p.Stage == SyncStage.Scanning);
        Assert.Contains(captured, p => p.Stage == SyncStage.Syncing && p.Processed == 2 && p.Total == 2 && p.CurrentPath == "two.txt");
        Assert.Equal(SyncStage.Completed, captured[^1].Stage);
    }

    private sealed class CaptureProgress(List<SyncProgress> values) : IProgress<SyncProgress>
    {
        public void Report(SyncProgress value) => values.Add(value);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
