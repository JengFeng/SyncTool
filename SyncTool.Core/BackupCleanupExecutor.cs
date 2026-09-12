namespace SyncTool.Core;

public static class BackupCleanupExecutor
{
    public static int Execute(BackupCleanupPreview preview, string auditPath)
    {
        var removed = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(auditPath)!);
        foreach (var item in preview.Items)
        {
            if (!File.Exists(item.Path)) continue;
            File.Delete(item.Path);
            File.AppendAllText(auditPath, $"{DateTimeOffset.UtcNow:O}\tBACKUP-CLEANUP\t{item.Length}\t{item.Path}{Environment.NewLine}");
            removed++;
        }
        return removed;
    }
}
