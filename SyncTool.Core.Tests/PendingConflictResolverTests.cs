using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class PendingConflictResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_a_to_b_backs_up_b_replaces_b_and_removes_queue_item()
    {
        var a = Create("A"); var b = Create("B"); var backups = Create("backups"); var queue = Path.Combine(_root, "pending.json");
        File.WriteAllText(Path.Combine(a, "same.txt"), "from A"); File.WriteAllText(Path.Combine(b, "same.txt"), "from B");
        var store = new PendingConflictStore(queue); store.Add(new PendingConflictItem("same.txt", DateTimeOffset.UtcNow, 6, 6));
        PendingConflictResolver.Resolve(a, b, backups, store, "same.txt", PendingConflictResolution.AToB);
        Assert.Equal("from A", File.ReadAllText(Path.Combine(b, "same.txt")));
        Assert.Contains("from B", Directory.EnumerateFiles(backups, "same.txt", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Empty(store.Load());
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
