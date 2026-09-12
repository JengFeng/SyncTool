using System.Text.Json;

namespace SyncTool.Core;

public sealed record PendingConflictItem(string RelativePath, DateTimeOffset DetectedAt, long ALength, long BLength);

public sealed class PendingConflictStore
{
    private readonly string _path;
    public PendingConflictStore(string path) => _path = path;

    public IReadOnlyList<PendingConflictItem> Load()
    {
        if (!File.Exists(_path)) return [];
        try { return JsonSerializer.Deserialize<List<PendingConflictItem>>(File.ReadAllText(_path)) ?? []; }
        catch { throw new InvalidOperationException("待決衝突清單毀損，請檢查 diagnostics。 "); }
    }

    public void Add(PendingConflictItem item)
    {
        var all = Load().Where(x => !x.RelativePath.Equals(item.RelativePath, StringComparison.OrdinalIgnoreCase)).Append(item).OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, true);
    }

    public void Remove(string relativePath)
    {
        var remaining = Load().Where(x => !x.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase)).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(remaining, new JsonSerializerOptions { WriteIndented = true }));
    }
}
