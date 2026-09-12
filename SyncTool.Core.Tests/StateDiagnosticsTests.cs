using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class StateDiagnosticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Corrupt_state_fails_closed_and_preserves_diagnostic_without_copying()
    {
        var a = Create("A"); var b = Create("B"); var state = Path.Combine(_root, "state", "two-way.json"); Directory.CreateDirectory(Path.GetDirectoryName(state)!);
        await File.WriteAllTextAsync(Path.Combine(a, "only-a.txt"), "safe"); await File.WriteAllTextAsync(state, "bad-state");
        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b, StateFilePath = state }).RunAsync(false);
        Assert.False(result.Success); Assert.False(File.Exists(Path.Combine(b, "only-a.txt")));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "state", "diagnostics"), "two-way-*.json"));
    }

    private string Create(string name) { var path = Path.Combine(_root, name); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
