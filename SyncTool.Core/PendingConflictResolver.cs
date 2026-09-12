namespace SyncTool.Core;

public enum PendingConflictResolution { AToB, BToA, KeepBoth, Skip }

public static class PendingConflictResolver
{
    public static void Resolve(string sourceRoot, string targetRoot, string backupRoot, PendingConflictStore store, string relativePath, PendingConflictResolution resolution)
    {
        if (resolution == PendingConflictResolution.Skip) return;
        var a = Path.Combine(sourceRoot, relativePath); var b = Path.Combine(targetRoot, relativePath);
        if (!File.Exists(a) || !File.Exists(b)) throw new FileNotFoundException("待決衝突的 A 或 B 檔案已不存在，請重新掃描。");
        if (resolution == PendingConflictResolution.KeepBoth)
        {
            CopyAtomically(a, ConflictCopy(sourceRoot, relativePath, "本機A側"));
            CopyAtomically(b, ConflictCopy(targetRoot, relativePath, "GDriveB側"));
            store.Remove(relativePath); return;
        }
        var from = resolution == PendingConflictResolution.AToB ? a : b;
        var to = resolution == PendingConflictResolution.AToB ? b : a;
        var backup = Path.Combine(backupRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"), relativePath);
        CopyAtomically(to, backup);
        CopyAtomically(from, to);
        store.Remove(relativePath);
    }

    private static void CopyAtomically(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temp = destination + "." + Guid.NewGuid().ToString("N") + ".synctmp";
        try { File.Copy(source, temp, false); File.Move(temp, destination, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string ConflictCopy(string root, string relativePath, string side)
    {
        var folder = Path.GetDirectoryName(Path.Combine(root, relativePath))!; var name = Path.GetFileNameWithoutExtension(relativePath); var ext = Path.GetExtension(relativePath);
        return Path.Combine(folder, $"{name}_衝突_{side}_{DateTime.UtcNow:yyyyMMdd-HHmmssfff}{ext}");
    }
}
