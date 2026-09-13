using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class EmptyDirectorySyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Two_way_sync_mirrors_empty_directories_but_ignores_desktop_ini()
    {
        var a = Path.Combine(_root, "a");
        var b = Path.Combine(_root, "b");
        Directory.CreateDirectory(Path.Combine(a, "from-a-empty"));
        Directory.CreateDirectory(Path.Combine(b, "from-b-desktop-only"));
        await File.WriteAllTextAsync(Path.Combine(b, "from-b-desktop-only", "desktop.ini"), "ignored");

        var result = await new SyncEngine(new SyncOptions
        {
            SourcePath = a, TargetPath = b, Mode = SyncMode.TwoWay,
            StateFilePath = Path.Combine(_root, "state.json"), BackupRootPath = Path.Combine(_root, "backups")
        }).RunAsync(dryRun: false);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(b, "from-a-empty")));
        Assert.True(Directory.Exists(Path.Combine(a, "from-b-desktop-only")));
        Assert.False(File.Exists(Path.Combine(a, "from-b-desktop-only", "desktop.ini")));
        Assert.Contains(result.Events, entry => entry == "ADD-DIRECTORY: from-a-empty -> B" || entry == "ADD-DIRECTORY: from-b-desktop-only -> A");

        Directory.Delete(Path.Combine(a, "from-a-empty"));
        var deletion = await new SyncEngine(new SyncOptions
        {
            SourcePath = a, TargetPath = b, Mode = SyncMode.TwoWay,
            StateFilePath = Path.Combine(_root, "state.json"), BackupRootPath = Path.Combine(_root, "backups")
        }).RunAsync(dryRun: false);

        Assert.True(deletion.Success);
        Assert.False(Directory.Exists(Path.Combine(b, "from-a-empty")));
        Assert.Contains(deletion.Events, entry => entry == "DELETE-EMPTY-DIRECTORY: from-a-empty -> B");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
