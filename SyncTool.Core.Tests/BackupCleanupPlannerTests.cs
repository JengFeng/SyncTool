using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class BackupCleanupPlannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Preview_lists_only_backups_older_than_retention_without_deleting()
    {
        Directory.CreateDirectory(_root);
        var old = Path.Combine(_root, "old.txt"); var recent = Path.Combine(_root, "recent.txt");
        File.WriteAllText(old, "old"); File.WriteAllText(recent, "recent");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-91)); File.SetLastWriteTimeUtc(recent, DateTime.UtcNow.AddDays(-2));
        var preview = BackupCleanupPlanner.Preview(_root, 90, DateTimeOffset.UtcNow);
        Assert.Single(preview.Items); Assert.Equal(old, preview.Items[0].Path); Assert.True(File.Exists(old));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
