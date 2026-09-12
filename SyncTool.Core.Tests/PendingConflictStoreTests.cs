using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class PendingConflictStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Add_deduplicates_relative_path_and_persists_decision_queue()
    {
        var store = new PendingConflictStore(Path.Combine(_root, "pending-conflicts.json"));
        store.Add(new PendingConflictItem("same.txt", DateTimeOffset.UtcNow, 10, 20));
        store.Add(new PendingConflictItem("same.txt", DateTimeOffset.UtcNow.AddMinutes(1), 11, 21));
        var items = store.Load();
        Assert.Single(items); Assert.Equal(11, items[0].ALength); Assert.True(File.Exists(Path.Combine(_root, "pending-conflicts.json")));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
