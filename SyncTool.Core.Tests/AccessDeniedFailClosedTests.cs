using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class AccessDeniedFailClosedTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsync_unreadable_source_file_fails_closed_without_writing_state_or_destination()
    {
        var a = Create("A");
        var b = Create("B");
        var state = Path.Combine(Create("state"), "manifest.json");
        var denied = Path.Combine(a, "cloud-only.pptx");
        await File.WriteAllTextAsync(denied, "fixture");

        var result = await new SyncEngine(new SyncOptions {
            SourcePath = a,
            TargetPath = b,
            StateFilePath = state,
            ReadAccessValidator = path => !string.Equals(path, denied, StringComparison.OrdinalIgnoreCase),
        }).RunAsync(dryRun: false);

        Assert.False(result.Success);
        Assert.Equal("SYNC_FILE_ACCESS_DENIED", result.ErrorMessage);
        Assert.Equal(1, result.AccessDeniedCount);
        Assert.Equal(0, result.AddCount);
        Assert.False(File.Exists(Path.Combine(b, "cloud-only.pptx")));
        Assert.False(File.Exists(state));
        Assert.DoesNotContain(result.Events, item => item.Contains(denied, StringComparison.OrdinalIgnoreCase));
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
