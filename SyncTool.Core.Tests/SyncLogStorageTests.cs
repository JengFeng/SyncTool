using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SyncLogStorageTests
{
    [Fact]
    public void GetPaths_keeps_per_job_log_and_adds_executable_root_log()
    {
        var paths = SyncLogStorage.GetPaths("C:\\SyncTool", "job-42", new DateTime(2026, 9, 9, 8, 0, 0));

        Assert.Equal("C:\\SyncTool\\jobs\\job-42\\logs\\sync-20260909.log", paths.JobLogPath);
        Assert.Equal("C:\\SyncTool\\logs\\sync-20260909.log", paths.RootLogPath);
    }
}
