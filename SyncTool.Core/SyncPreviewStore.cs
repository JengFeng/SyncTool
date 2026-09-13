using System.Text.Json;

namespace SyncTool.Core;

public sealed record SyncPreview(string Id, string JobId, SyncMode Mode, string SourcePath, string TargetPath, string Fingerprint, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed class SyncPreviewStore
{
    private readonly string _root;
    public SyncPreviewStore(string root) => _root = root;

    public SyncPreview Create(string jobId, SyncMode mode, string sourcePath, string targetPath, string fingerprint, TimeSpan? lifetime = null)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(lifetime ?? TimeSpan.FromMinutes(30));
        if (expiresAt <= now || expiresAt > now.AddHours(1)) throw new ArgumentOutOfRangeException(nameof(lifetime), "預覽有效期必須介於 1 秒與 1 小時。");
        var preview = new SyncPreview(Guid.NewGuid().ToString("N"), jobId, mode, Normalize(sourcePath), Normalize(targetPath), fingerprint, now, expiresAt);
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, preview.Id + ".json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
        return preview;
    }

    public bool TryValidate(string previewId, SyncMode mode, string sourcePath, string targetPath, string fingerprint, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(previewId)) { error = "同步需要有效的預覽 ID。"; return false; }
        var path = Path.Combine(_root, previewId + ".json");
        if (!File.Exists(path)) { error = "找不到預覽，請重新建立。"; return false; }
        SyncPreview? preview;
        try { preview = JsonSerializer.Deserialize<SyncPreview>(File.ReadAllText(path)); }
        catch
        {
            PreserveDiagnostic(path, previewId);
            error = "預覽檔毀損，已保留 diagnostics，請重新建立。"; return false;
        }
        if (preview is null || preview.Id != previewId) { error = "預覽資料無效，請重新建立。"; return false; }
        if (preview.ExpiresAt < DateTimeOffset.UtcNow) { error = "預覽已逾期，請重新建立。"; return false; }
        if (preview.Mode != mode || preview.SourcePath != Normalize(sourcePath) || preview.TargetPath != Normalize(targetPath)) { error = "預覽與目前工作模式或路徑不相符。"; return false; }
        if (!string.Equals(preview.Fingerprint, fingerprint, StringComparison.Ordinal)) { error = "預覽後資料已變更，請重新建立。"; return false; }
        return true;
    }

    private void PreserveDiagnostic(string sourcePath, string previewId)
    {
        try
        {
            var diagnostics = Path.Combine(_root, "diagnostics"); Directory.CreateDirectory(diagnostics);
            var destination = Path.Combine(diagnostics, $"{previewId}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json");
            var temporary = destination + ".tmp"; File.Copy(sourcePath, temporary, true); File.Move(temporary, destination, true);
        }
        catch { /* diagnostics must never weaken fail-closed validation */ }
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
