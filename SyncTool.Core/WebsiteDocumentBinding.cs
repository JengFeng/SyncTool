using System.Text.Json;

namespace SyncTool.Core;

public sealed record WebsiteDocumentBinding(string DocumentLibraryRoot);

public static class WebsiteDocumentBindingStore
{
    public const string BindingFileName = "document-library-binding.json";

    public static WebsiteDocumentBinding Read(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !Path.IsPathFullyQualified(configPath)) throw new InvalidOperationException("網站文件庫設定檔路徑無效。");
        var fullConfigPath = Path.GetFullPath(configPath);
        if (!string.Equals(Path.GetFileName(fullConfigPath), BindingFileName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("網站文件庫設定檔必須命名為 document-library-binding.json。");
        if (!File.Exists(fullConfigPath) || HasReparseComponent(fullConfigPath)) throw new InvalidOperationException("網站文件庫設定檔無法安全讀取。");
        var info = new FileInfo(fullConfigPath);
        if (info.Length is <= 0 or > 4096) throw new InvalidOperationException("網站文件庫設定檔格式無效。");
        JsonDocument document;
        try { document = JsonDocument.Parse(File.ReadAllText(fullConfigPath)); }
        catch (JsonException) { throw new InvalidOperationException("網站文件庫設定檔格式無效。"); }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || document.RootElement.EnumerateObject().Count() != 1
                || !document.RootElement.TryGetProperty("documentLibraryRoot", out var rootValue)
                || rootValue.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("網站文件庫設定檔格式無效。");
            var root = rootValue.GetString()?.Trim() ?? "";
            if (root.Length == 0 || !Path.IsPathFullyQualified(root)) throw new InvalidOperationException("網站文件庫根目錄無效。");
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(fullRoot) || HasReparseComponent(fullRoot)) throw new InvalidOperationException("網站文件庫根目錄無效。");
            return new WebsiteDocumentBinding(fullRoot);
        }
    }

    public static void Write(string configPath, string documentLibraryRoot)
    {
        if (string.IsNullOrWhiteSpace(configPath) || !Path.IsPathFullyQualified(configPath)) throw new InvalidOperationException("網站文件庫設定檔路徑無效。");
        if (string.IsNullOrWhiteSpace(documentLibraryRoot) || !Path.IsPathFullyQualified(documentLibraryRoot)) throw new InvalidOperationException("網站文件庫根目錄無效。");
        var root = Path.GetFullPath(documentLibraryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(root) || HasReparseComponent(root)) throw new InvalidOperationException("網站文件庫根目錄無效。");
        var fullConfigPath = Path.GetFullPath(configPath);
        if (!string.Equals(Path.GetFileName(fullConfigPath), BindingFileName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("網站文件庫設定檔必須命名為 document-library-binding.json。");
        var parent = Path.GetDirectoryName(fullConfigPath);
        if (string.IsNullOrWhiteSpace(parent)) throw new InvalidOperationException("網站文件庫設定檔路徑無效。");
        Directory.CreateDirectory(parent);
        if (HasReparseComponent(parent) || (File.Exists(fullConfigPath) && HasReparseComponent(fullConfigPath))) throw new InvalidOperationException("網站文件庫設定檔無法安全寫入。");
        var payload = JsonSerializer.Serialize(new { documentLibraryRoot = root }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        var temporary = Path.Combine(parent, ".binding-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, payload);
            File.Move(temporary, fullConfigPath, true);
            if (!string.Equals(Read(fullConfigPath).DocumentLibraryRoot, root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("網站文件庫設定檔無法安全寫入。");
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static bool HasReparseComponent(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root)) return true;
        var relative = fullPath[root.Length..];
        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) return true;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }
}
