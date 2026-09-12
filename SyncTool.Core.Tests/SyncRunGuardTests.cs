using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SyncRunGuardTests
{
    [Fact]
    public void Begin_locks_job_selection_and_keeps_running_job_stable()
    {
        var guard = new SyncRunGuard();

        guard.Begin("water-job");

        Assert.True(guard.IsRunning);
        Assert.False(guard.CanChangeSelectedJob);
        Assert.Equal("water-job", guard.RunningJobId);
    }

    [Fact]
    public void End_unlocks_job_selection_and_clears_running_job()
    {
        var guard = new SyncRunGuard();
        guard.Begin("water-job");

        guard.End();

        Assert.False(guard.IsRunning);
        Assert.True(guard.CanChangeSelectedJob);
        Assert.Null(guard.RunningJobId);
    }
}
