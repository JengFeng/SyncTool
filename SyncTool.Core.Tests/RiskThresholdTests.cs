using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class RiskThresholdTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Directional_preview_requires_approval_when_overwrite_or_removal_exceeds_limit()
    {
        var a = Create("A"); var b = Create("B");
        await File.WriteAllTextAsync(Path.Combine(a, "same.txt"), "new A content");
        await File.WriteAllTextAsync(Path.Combine(b, "same.txt"), "old B");
        await File.WriteAllTextAsync(Path.Combine(b, "extra.txt"), "extra");
        File.SetLastWriteTimeUtc(Path.Combine(a, "same.txt"), DateTime.UtcNow.AddMinutes(1));
        var result = await new SyncEngine(new SyncOptions { SourcePath = a, TargetPath = b, Mode = SyncMode.AToB, RiskOperationThreshold = 1 }).RunAsync(true);
        Assert.True(result.RequiresApproval); Assert.Equal(2, result.RiskOperationCount); Assert.True(result.RiskBytes > 0);
    }

    private string Create(string relative) { var path = Path.Combine(_root, relative); Directory.CreateDirectory(path); return path; }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
