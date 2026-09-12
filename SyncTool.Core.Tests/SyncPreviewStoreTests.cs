using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SyncPreviewStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Preview_is_valid_only_when_mode_paths_and_fingerprint_match()
    {
        var store = new SyncPreviewStore(Path.Combine(_root, "previews"));
        var preview = store.Create("job", SyncMode.AToB, "C:\\A", "D:\\B", "fingerprint");

        Assert.True(store.TryValidate(preview.Id, SyncMode.AToB, "C:\\A", "D:\\B", "fingerprint", out _));
        Assert.False(store.TryValidate(preview.Id, SyncMode.AToB, "C:\\A", "D:\\B", "changed", out var error));
        Assert.Contains("變更", error);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
