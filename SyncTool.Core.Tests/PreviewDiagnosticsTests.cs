using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class PreviewDiagnosticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Corrupt_preview_is_preserved_in_diagnostics_before_validation_fails()
    {
        var previews = Path.Combine(_root, "previews"); Directory.CreateDirectory(previews);
        File.WriteAllText(Path.Combine(previews, "bad.json"), "not valid json");
        var store = new SyncPreviewStore(previews);
        Assert.False(store.TryValidate("bad", SyncMode.AToB, Path.Combine(_root, "A"), Path.Combine(_root, "B"), "fingerprint", out _));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(previews, "diagnostics"), "bad-*.json"));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
