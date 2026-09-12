namespace SyncTool.Core;

public sealed record BackupCleanupItem(string Path, long Length, DateTimeOffset LastWriteUtc);
public sealed record BackupCleanupPreview(IReadOnlyList<BackupCleanupItem> Items, long TotalBytes);

public static class BackupCleanupPlanner
{
    public static BackupCleanupPreview Preview(string backupRoot, int retentionDays, DateTimeOffset now)
    {
        if (!Directory.Exists(backupRoot)) return new([], 0);
        var cutoff = now.UtcDateTime.AddDays(-retentionDays);
        var items = Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(info => info.LastWriteTimeUtc < cutoff)
            .Select(info => new BackupCleanupItem(info.FullName, info.Length, new DateTimeOffset(info.LastWriteTimeUtc)))
            .OrderBy(x => x.LastWriteUtc).ToList();
        return new(items, items.Sum(x => x.Length));
    }
}
