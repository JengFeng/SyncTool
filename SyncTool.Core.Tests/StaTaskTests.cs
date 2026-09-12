using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class StaTaskTests
{
    [Fact]
    public async Task RunAsync_does_not_block_caller_while_picker_work_is_waiting()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        var task = StaTask.RunAsync(() =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            return "C:\\chosen";
        });

        Assert.True(entered.Wait(TimeSpan.FromSeconds(1)));
        Assert.False(task.IsCompleted);
        release.Set();
        Assert.Equal("C:\\chosen", await task);
    }
}
