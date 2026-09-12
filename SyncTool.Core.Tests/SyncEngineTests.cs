using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SyncEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunAsync_initial_sync_copies_file_from_a_to_b_and_counts_addition()
    {
        var a = Path.Combine(_root, "A");
        var b = Path.Combine(_root, "B");
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        await File.WriteAllTextAsync(Path.Combine(a, "report.txt"), "source data");

        var engine = new SyncEngine(new SyncOptions
        {
            SourcePath = a,
            TargetPath = b,
            DeleteProtectionEnabled = true
        });

        var result = await engine.RunAsync(dryRun: false);

        Assert.True(result.Success);
        Assert.Equal(1, result.AddCount);
        Assert.Equal("source data", await File.ReadAllTextAsync(Path.Combine(b, "report.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
