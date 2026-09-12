using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class BackupCleanupExecutorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Confirmed_cleanup_deletes_only_previewed_items_and_writes_audit()
    {
        Directory.CreateDirectory(_root);
        var expired = Path.Combine(_root, "expired.txt"); var retained = Path.Combine(_root, "retained.txt"); var audit = Path.Combine(_root, "audit.log");
        File.WriteAllText(expired, "old"); File.WriteAllText(retained, "new");
        File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-91));
        var preview = BackupCleanupPlanner.Preview(_root, 90, DateTimeOffset.UtcNow);
        var removed = BackupCleanupExecutor.Execute(preview, audit);
        Assert.Equal(1, removed); Assert.False(File.Exists(expired)); Assert.True(File.Exists(retained)); Assert.Contains("BACKUP-CLEANUP", File.ReadAllText(audit));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
