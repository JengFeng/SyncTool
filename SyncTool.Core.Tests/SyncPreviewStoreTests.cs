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
        var webPreview = store.Create("web", SyncMode.TwoWay, "C:\\A", "D:\\B", "fingerprint", TimeSpan.FromMinutes(5));

        Assert.True(store.TryValidate(preview.Id, SyncMode.AToB, "C:\\A", "D:\\B", "fingerprint", out _));
        Assert.InRange((webPreview.ExpiresAt - webPreview.CreatedAt).TotalSeconds, 299, 301);
        Assert.False(store.TryValidate(preview.Id, SyncMode.AToB, "C:\\A", "D:\\B", "changed", out var error));
        Assert.Contains("變更", error);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
