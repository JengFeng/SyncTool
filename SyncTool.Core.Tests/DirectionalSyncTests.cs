using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class DirectionalSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AToB_updates_b_and_removes_extra_target_after_backup()
    {
        var a = Create("A"); var b = Create("B"); var backup = Create("backup");
        await File.WriteAllTextAsync(Path.Combine(a, "shared.txt"), "authoritative A");
        await File.WriteAllTextAsync(Path.Combine(b, "shared.txt"), "old B");
        await File.WriteAllTextAsync(Path.Combine(b, "extra.txt"), "remove me");

        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b, BackupRootPath = backup, Mode = SyncMode.AToB }).RunAsync(false);

        Assert.True(result.Success); Assert.Equal("authoritative A", await File.ReadAllTextAsync(Path.Combine(b, "shared.txt")));
        Assert.False(File.Exists(Path.Combine(b, "extra.txt")));
        Assert.Single(Directory.EnumerateFiles(backup, "extra.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AToB_overwrite_backs_up_old_target_and_leaves_no_temporary_file()
    {
        var a = Create("A"); var b = Create("B"); var backup = Create("backup");
        await File.WriteAllTextAsync(Path.Combine(a, "shared.txt"), "new authoritative content");
        await File.WriteAllTextAsync(Path.Combine(b, "shared.txt"), "old target content");
        File.SetLastWriteTimeUtc(Path.Combine(a, "shared.txt"), DateTime.UtcNow.AddMinutes(1));

        await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b, BackupRootPath = backup, Mode = SyncMode.AToB }).RunAsync(false);

        Assert.Equal("new authoritative content", await File.ReadAllTextAsync(Path.Combine(b, "shared.txt")));
        Assert.Contains("old target content", Directory.EnumerateFiles(backup, "shared.txt", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Empty(Directory.EnumerateFiles(b, "*.synctmp", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task Preview_mode_reports_changes_without_writing()
    {        var a = Create("A"); var b = Create("B");
        await File.WriteAllTextAsync(Path.Combine(a, "new.txt"), "data");

        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b, Mode = SyncMode.Preview }).RunAsync(false);

        Assert.True(result.Success); Assert.Equal(1, result.AddCount); Assert.False(File.Exists(Path.Combine(b, "new.txt")));
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
