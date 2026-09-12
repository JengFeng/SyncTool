using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsync_dry_run_reports_add_without_writing_destination()
    {
        var a = Create("A"); var b = Create("B");
        await File.WriteAllTextAsync(Path.Combine(a, "new.txt"), "data");
        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b }).RunAsync(dryRun: true);
        Assert.True(result.Success); Assert.Equal(1, result.AddCount); Assert.False(File.Exists(Path.Combine(b, "new.txt")));
    }

    [Fact]
    public async Task RunAsync_initial_conflict_creates_both_conflict_copies()
    {
        var a = Create("A"); var b = Create("B");
        await File.WriteAllTextAsync(Path.Combine(a, "same.txt"), "alpha");
        await File.WriteAllTextAsync(Path.Combine(b, "same.txt"), "bravo");
        File.SetLastWriteTimeUtc(Path.Combine(a, "same.txt"), DateTime.UtcNow.AddMinutes(-1));
        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b }).RunAsync(dryRun: false);
        Assert.True(result.Success); Assert.Equal(1, result.ConflictCount);
        Assert.Equal(2, Directory.EnumerateFiles(_root, "*衝突*", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public async Task RunAsync_rejects_nested_sync_paths()
    {
        var a = Create("A"); var b = Create(Path.Combine("A", "B"));
        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b }).RunAsync(dryRun: true);
        Assert.False(result.Success); Assert.Contains("獨立", result.ErrorMessage);
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
