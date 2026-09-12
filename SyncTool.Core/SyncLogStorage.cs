namespace SyncTool.Core;

public sealed record SyncLogPaths(string JobLogPath, string RootLogPath);

public static class SyncLogStorage
{
    public static SyncLogPaths GetPaths(string appRoot, string jobId, DateTime timestamp)
    {
        var filename = $"sync-{timestamp:yyyyMMdd}.log";
        return new SyncLogPaths(
            Path.Combine(appRoot, "jobs", jobId, "logs", filename),
            Path.Combine(appRoot, "logs", filename));
    }
}
