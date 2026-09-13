using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class TwoWayPreviewTokenTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Two_way_dry_run_issues_token_and_run_once_requires_that_token()
    {
        var a = Path.Combine(_root, "a"); var b = Path.Combine(_root, "b"); var previews = Path.Combine(_root, "previews");
        Directory.CreateDirectory(a); Directory.CreateDirectory(b);
        await File.WriteAllTextAsync(Path.Combine(a, "new.txt"), "new");
        var previewOptions = Options(a, b, previews);
        var preview = await new SyncEngine(previewOptions).RunAsync(dryRun: true);
        Assert.True(preview.Success);
        Assert.False(string.IsNullOrWhiteSpace(preview.PreviewId));
        Assert.NotNull(preview.PreviewExpiresAt);

        var withoutToken = await new SyncEngine(Options(a, b, previews)).RunAsync(dryRun: false);
        Assert.False(withoutToken.Success);
        Assert.Contains("預覽 ID", withoutToken.ErrorMessage);

        var confirmed = await new SyncEngine(Options(a, b, previews, preview.PreviewId)).RunAsync(dryRun: false);
        Assert.True(confirmed.Success);
        Assert.True(File.Exists(Path.Combine(b, "new.txt")));
    }

    private static SyncOptions Options(string a, string b, string previews, string? previewId = null) => new()
    {
        SourcePath = a, TargetPath = b, Mode = SyncMode.TwoWay, JobId = "web-job", PreviewRootPath = previews,
        RequiredPreviewId = previewId, StateFilePath = Path.Combine(Path.GetDirectoryName(previews)!, "state.json"),
        BackupRootPath = Path.Combine(Path.GetDirectoryName(previews)!, "backup")
    };

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
