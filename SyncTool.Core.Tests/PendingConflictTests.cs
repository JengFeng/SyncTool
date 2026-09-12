using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class PendingConflictTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Two_way_pause_policy_keeps_both_files_and_does_not_write_state()
    {
        var a = Create("A"); var b = Create("B"); var state = Path.Combine(_root, "state.json");
        await File.WriteAllTextAsync(Path.Combine(a, "same.txt"), "A"); await File.WriteAllTextAsync(Path.Combine(b, "same.txt"), "B");
        File.SetLastWriteTimeUtc(Path.Combine(a, "same.txt"), DateTime.UtcNow.AddMinutes(-2));
        File.SetLastWriteTimeUtc(Path.Combine(b, "same.txt"), DateTime.UtcNow);
        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b, StateFilePath = state, ConflictPolicy = ConflictPolicy.PauseForDecision }).RunAsync(false);
        Assert.True(result.Success, result.ErrorMessage); Assert.True(result.HasPendingConflicts); Assert.False(File.Exists(state));
        Assert.Equal("A", await File.ReadAllTextAsync(Path.Combine(a, "same.txt"))); Assert.Equal("B", await File.ReadAllTextAsync(Path.Combine(b, "same.txt")));
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
