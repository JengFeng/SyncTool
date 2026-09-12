using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class LifecycleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsync_delete_protection_archives_remaining_copy_after_one_side_deletes()
    {
        var a = Create("A"); var b = Create("B"); var backup = Create("backups"); var state = Path.Combine(_root, "sync-state.json");
        await File.WriteAllTextAsync(Path.Combine(a, "keep.txt"), "important");
        var options = new SyncOptions { SourcePath = a, TargetPath = b, BackupRootPath = backup, StateFilePath = state, DeleteProtectionEnabled = true };
        Assert.True((await new SyncEngine(options).RunAsync(false)).Success);
        File.Delete(Path.Combine(a, "keep.txt"));
        var result = await new SyncEngine(options).RunAsync(false);
        Assert.True(result.Success); Assert.Equal(1, result.DeleteCount);
        Assert.False(File.Exists(Path.Combine(b, "keep.txt")));
        Assert.Single(Directory.EnumerateFiles(backup, "keep.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RunAsync_reports_failure_when_target_mount_is_missing()
    {
        var a = Create("A"); var missing = Path.Combine(_root, "missing-drive");
        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = missing }).RunAsync(true);
        Assert.False(result.Success); Assert.Contains("不存在", result.ErrorMessage);
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
