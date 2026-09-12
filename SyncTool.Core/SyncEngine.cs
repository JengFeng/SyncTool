using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace SyncTool.Core;

public sealed class SyncOptions
{
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public bool DeleteProtectionEnabled { get; init; } = true;
    public bool ConflictCopiesEnabled { get; init; } = true;
    public bool LockRetryEnabled { get; init; } = true;
    public SyncMode Mode { get; init; } = SyncMode.TwoWay;
    public bool PreserveTargetExtras { get; init; }
    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.AutoKeepCopies;
    public int RiskOperationThreshold { get; init; } = 100;
    public long RiskBytesThreshold { get; init; } = 10L * 1024 * 1024 * 1024;
    public string? BackupRootPath { get; init; }
    public string? StateFilePath { get; init; }
    public string? PreviewRootPath { get; init; }
    public string? JobId { get; init; }
    public string? RequiredPreviewId { get; init; }
    public string? PendingConflictPath { get; init; }
    public IReadOnlyList<string> IgnorePatterns { get; init; } = Array.Empty<string>();
}

public sealed class SyncResult
{
    public bool Success { get; set; }
    public DateTimeOffset SyncTime { get; init; } = DateTimeOffset.Now;
    public int AddCount { get; set; }
    public int UpdateCount { get; set; }
    public int DeleteCount { get; set; }
    public int ConflictCount { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string? PreviewId { get; set; }
    public DateTimeOffset? PreviewExpiresAt { get; set; }
    public bool HasPendingConflicts { get; set; }
    public int RiskOperationCount { get; set; }
    public long RiskBytes { get; set; }
    public bool RequiresApproval { get; set; }
    public List<string> Events { get; } = [];
}

public sealed class SyncEngine
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string[] BuiltInIgnoredNames = ["desktop.ini", "thumbs.db"];

    private readonly SyncOptions _options;
    public SyncEngine(SyncOptions options) => _options = options;

    public async Task<SyncResult> RunAsync(bool dryRun, IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        try
        {
            ValidatePaths();
            await Gate.WaitAsync(cancellationToken);
            try
            {
                progress?.Report(new(SyncStage.Scanning, 0, 0, "", "正在掃描來源 A 與目標 B…"));
                var aFiles = ScanFiles(_options.SourcePath);
                var bFiles = ScanFiles(_options.TargetPath);
                var previous = LoadState();
                var effectiveDryRun = dryRun || _options.Mode == SyncMode.Preview;
                var fingerprint = ScanFingerprint(aFiles, bFiles);
                if ((_options.Mode is SyncMode.AToB or SyncMode.BToA) && !string.IsNullOrWhiteSpace(_options.PreviewRootPath))
                {
                    var previews = new SyncPreviewStore(_options.PreviewRootPath!);
                    if (effectiveDryRun)
                    {
                        var preview = previews.Create(_options.JobId ?? "standalone", _options.Mode, _options.SourcePath, _options.TargetPath, fingerprint);
                        result.PreviewId = preview.Id; result.PreviewExpiresAt = preview.ExpiresAt;
                        result.Events.Add($"PREVIEW: {preview.Id} expires={preview.ExpiresAt:O}");
                    }
                    else if (!previews.TryValidate(_options.RequiredPreviewId ?? "", _options.Mode, _options.SourcePath, _options.TargetPath, fingerprint, out var previewError))
                    {
                        throw new InvalidOperationException(previewError);
                    }
                }
                var all = aFiles.Keys.Union(bFiles.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                var processed = 0;
                progress?.Report(new(SyncStage.Syncing, 0, all.Count, "", $"已掃描 {all.Count:N0} 個項目，開始同步…"));

                foreach (var relative in all)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    processed++;
                    progress?.Report(new(SyncStage.Syncing, processed, all.Count, relative, "同步中"));
                    aFiles.TryGetValue(relative, out var a);
                    bFiles.TryGetValue(relative, out var b);
                    previous.TryGetValue(relative, out var old);

                    if (_options.Mode is SyncMode.AToB or SyncMode.BToA)
                    {
                        await HandleDirectionalAsync(relative, a, b, _options.Mode == SyncMode.AToB, effectiveDryRun, result, cancellationToken);
                    }
                    else if (a is null)
                        await HandleMissingAsync(relative, b!, old?.A, false, effectiveDryRun, result, cancellationToken);
                    else if (b is null)
                        await HandleMissingAsync(relative, a, old?.B, true, effectiveDryRun, result, cancellationToken);
                    else if (!Equivalent(a, b))
                        await HandleDifferenceAsync(relative, a, b, old, effectiveDryRun, result, cancellationToken);
                }

                result.RequiresApproval = effectiveDryRun && (_options.Mode is SyncMode.AToB or SyncMode.BToA) && (result.RiskOperationCount > _options.RiskOperationThreshold || result.RiskBytes > _options.RiskBytesThreshold);
                if (result.RequiresApproval) result.Events.Add($"RISK-APPROVAL-REQUIRED: operations={result.RiskOperationCount} bytes={result.RiskBytes}");
                if (!effectiveDryRun && !result.HasPendingConflicts) SaveState();
                result.Success = true;
                progress?.Report(new(SyncStage.Completed, processed, all.Count, "", "同步完成"));
            }
            finally { Gate.Release(); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Events.Add($"ERROR: {ex.Message}");
            progress?.Report(new(SyncStage.Failed, 0, 0, "", ex.Message));
        }
        return result;
    }

    private async Task HandleDirectionalAsync(string relative, FileEntry? a, FileEntry? b, bool aToB, bool dryRun, SyncResult result, CancellationToken ct)
    {
        var authoritative = aToB ? a : b;
        var target = aToB ? b : a;
        var fromRoot = aToB ? _options.SourcePath : _options.TargetPath;
        var toRoot = aToB ? _options.TargetPath : _options.SourcePath;
        if (authoritative is not null)
        {
            if (target is null)
            {
                if (await CopyAsync(Path.Combine(fromRoot, relative), Path.Combine(toRoot, relative), dryRun, ct, result)) { result.AddCount++; result.Events.Add($"ADD-{SyncModeNames.Key(_options.Mode)}: {relative}"); }
            }
            else if (!Equivalent(authoritative, target) && await CopyAsync(Path.Combine(fromRoot, relative), Path.Combine(toRoot, relative), dryRun, ct, result))
            {
                result.RiskOperationCount++; result.RiskBytes += Math.Max(authoritative.Length, target.Length);
                result.UpdateCount++; result.Events.Add($"UPDATE-{SyncModeNames.Key(_options.Mode)}: {relative}");
            }
            return;
        }
        if (target is null || _options.PreserveTargetExtras) return;
        var targetPath = Path.Combine(toRoot, relative);
        var backup = BackupPath(relative);
        if (await CopyAsync(targetPath, backup, dryRun, ct, result))
        {
            result.RiskOperationCount++; result.RiskBytes += target.Length;
            if (!dryRun) File.Delete(targetPath);
            result.DeleteCount++; result.Events.Add($"DELETE-MIRROR-{SyncModeNames.Key(_options.Mode)}: {relative} -> {backup}");
        }
    }

    private async Task HandleMissingAsync(string relative, FileEntry existing, FileStamp? previousMissingSide, bool sourceHasFile, bool dryRun, SyncResult result, CancellationToken ct)
    {
        // No manifest entry means this is an initial single-sided addition.
        if (previousMissingSide is null)
        {
            var from = sourceHasFile ? _options.SourcePath : _options.TargetPath;
            var to = sourceHasFile ? _options.TargetPath : _options.SourcePath;
            if (await CopyAsync(Path.Combine(from, relative), Path.Combine(to, relative), dryRun, ct, result))
            {
                result.AddCount++;
                result.Events.Add($"ADD: {relative}");
            }
            return;
        }

        if (_options.DeleteProtectionEnabled)
        {
            var existingPath = Path.Combine(sourceHasFile ? _options.SourcePath : _options.TargetPath, relative);
            var backup = BackupPath(relative);
            if (await CopyAsync(existingPath, backup, dryRun, ct, result))
            {
                if (!dryRun) File.Delete(existingPath);
                result.DeleteCount++;
                result.Events.Add($"DELETE-PROTECTED: {relative} -> {backup}");
            }
        }
        else
        {
            var from = sourceHasFile ? _options.SourcePath : _options.TargetPath;
            var to = sourceHasFile ? _options.TargetPath : _options.SourcePath;
            if (await CopyAsync(Path.Combine(from, relative), Path.Combine(to, relative), dryRun, ct, result))
            {
                result.AddCount++;
                result.Events.Add($"RESTORE: {relative}");
            }
        }
    }

    private async Task HandleDifferenceAsync(string relative, FileEntry a, FileEntry b, SyncPair? old, bool dryRun, SyncResult result, CancellationToken ct)
    {
        var aChanged = old is null || old.A is null || !Equivalent(a, old.A);
        var bChanged = old is null || old.B is null || !Equivalent(b, old.B);
        if (aChanged && bChanged && _options.ConflictPolicy == ConflictPolicy.PauseForDecision)
        {
            if (!string.IsNullOrWhiteSpace(_options.PendingConflictPath)) new PendingConflictStore(_options.PendingConflictPath).Add(new PendingConflictItem(relative, DateTimeOffset.UtcNow, a.Length, b.Length));
            result.ConflictCount++; result.HasPendingConflicts = true; result.Events.Add($"PENDING-CONFLICT: {relative}"); return;
        }
        if (aChanged && bChanged && _options.ConflictCopiesEnabled)
        {
            var aConflict = ConflictPath(_options.SourcePath, relative, "本機A側");
            var bConflict = ConflictPath(_options.TargetPath, relative, "GDriveB側");
            var aSaved = await CopyAsync(Path.Combine(_options.SourcePath, relative), aConflict, dryRun, ct, result);
            var bSaved = await CopyAsync(Path.Combine(_options.TargetPath, relative), bConflict, dryRun, ct, result);
            if (aSaved && bSaved)
            {
                result.ConflictCount++;
                result.Events.Add($"CONFLICT: {relative}");
            }
            return;
        }

        var copyAtoB = a.LastWriteUtc >= b.LastWriteUtc;
        var fromRoot = copyAtoB ? _options.SourcePath : _options.TargetPath;
        var toRoot = copyAtoB ? _options.TargetPath : _options.SourcePath;
        if (await CopyAsync(Path.Combine(fromRoot, relative), Path.Combine(toRoot, relative), dryRun, ct, result))
        {
            result.UpdateCount++;
            result.Events.Add($"UPDATE: {relative}");
        }
    }

    private static bool Equivalent(FileEntry left, FileEntry right) => left.Length == right.Length && Math.Abs((left.LastWriteUtc - right.LastWriteUtc).TotalSeconds) <= 2;
    private static bool Equivalent(FileEntry left, FileStamp right) => left.Length == right.Length && Math.Abs((left.LastWriteUtc - right.LastWriteUtc).TotalSeconds) <= 2;

    private void BackupExistingDestination(string destination, bool dryRun)
    {
        if (dryRun || !File.Exists(destination)) return;
        var root = destination.StartsWith(_options.SourcePath, StringComparison.OrdinalIgnoreCase) ? _options.SourcePath : destination.StartsWith(_options.TargetPath, StringComparison.OrdinalIgnoreCase) ? _options.TargetPath : null;
        if (root is null) return;
        var backup = BackupPath(Path.GetRelativePath(root, destination));
        var i = 2;
        while (File.Exists(backup)) backup = Path.Combine(Path.GetDirectoryName(backup)!, $"{Path.GetFileNameWithoutExtension(backup)}_{i++}{Path.GetExtension(backup)}");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Copy(destination, backup, overwrite: false);
    }

    private async Task<bool> CopyAsync(string source, string destination, bool dryRun, CancellationToken ct, SyncResult result)
    {
        if (dryRun) return true;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            BackupExistingDestination(destination, dryRun);
            var retryDelays = _options.LockRetryEnabled ? new[] { 0, 5000, 15000, 30000 } : new[] { 0 };
            Exception? last = null;
            foreach (var delay in retryDelays)
            {
                if (delay > 0) await Task.Delay(delay, ct);
                var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".synctmp";
                try
                {
                    File.Copy(source, temporary, overwrite: false);
                    if (new FileInfo(source).Length != new FileInfo(temporary).Length) throw new IOException("暫存檔大小驗證失敗。");
                    if (!await ContentHashMatchesAsync(source, temporary, ct)) throw new IOException("暫存檔 SHA-256 驗證失敗。");
                    File.Move(temporary, destination, overwrite: true);
                    return true;
                }
                catch (IOException ex) { last = ex; }
                catch (UnauthorizedAccessException ex)
                {
                    result.Events.Add($"WARNING: 無權存取，已略過並保留至下輪重試：{source} ({ex.Message})");
                    return false;
                }
                finally
                {
                    if (File.Exists(temporary)) { try { File.Delete(temporary); } catch { } }
                }
            }
            result.Events.Add($"WARNING: 檔案鎖定或無法複製，已略過並保留至下輪重試：{source} ({last?.Message})");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Events.Add($"WARNING: 無權建立目標路徑，已略過並保留至下輪重試：{destination} ({ex.Message})");
            return false;
        }
        catch (DirectoryNotFoundException ex)
        {
            result.Events.Add($"WARNING: 路徑已變更，已略過並保留至下輪重試：{source} ({ex.Message})");
            return false;
        }
    }

    private static async Task<bool> ContentHashMatchesAsync(string first, string second, CancellationToken ct)
    {
        await using var left = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var right = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var leftHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var rightHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var leftBuffer = new byte[1024 * 1024]; var rightBuffer = new byte[1024 * 1024];
        while (true)
        {
            var leftRead = await left.ReadAsync(leftBuffer, ct); var rightRead = await right.ReadAsync(rightBuffer, ct);
            if (leftRead != rightRead) return false;
            if (leftRead == 0) break;
            leftHash.AppendData(leftBuffer, 0, leftRead); rightHash.AppendData(rightBuffer, 0, rightRead);
        }
        return CryptographicOperations.FixedTimeEquals(leftHash.GetHashAndReset(), rightHash.GetHashAndReset());
    }

    private static string ScanFingerprint(Dictionary<string, FileEntry> aFiles, Dictionary<string, FileEntry> bFiles)
    {
        var rows = aFiles.Keys.Union(bFiles.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(path => string.Join("|", path, Stamp(aFiles.GetValueOrDefault(path)), Stamp(bFiles.GetValueOrDefault(path))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
    }

    private static string Stamp(FileEntry? entry) => entry is null ? "-" : $"{entry.Length}:{entry.LastWriteUtc.Ticks}";

    private Dictionary<string, FileEntry> ScanFiles(string root)
    {
        var map = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (var path in Directory.EnumerateFiles(root, "*", enumeration))
        {
            // Check the raw leaf name before FileInfo touches Windows device aliases such as NUL.
            if (WindowsPathRules.IsReservedDeviceName(Path.GetFileName(path))) continue;
            try
            {
                var info = new FileInfo(path);
                if (ShouldIgnore(info, root)) continue;
                var relative = Path.GetRelativePath(root, path);
                map[relative] = new FileEntry(info.Length, info.LastWriteTimeUtc);
            }
            catch (IOException) { /* inaccessible/special item: leave it out of this sync pass */ }
            catch (UnauthorizedAccessException) { /* access warning is non-fatal */ }
            catch (ArgumentException) { /* malformed Windows path: ignore safely */ }
        }
        return map;
    }

    private bool ShouldIgnore(FileInfo info, string root)
    {
        if (info.Attributes.HasFlag(FileAttributes.Hidden) || info.Attributes.HasFlag(FileAttributes.System) || info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return true;
        var file = info.Name;
        if (BuiltInIgnoredNames.Contains(file, StringComparer.OrdinalIgnoreCase) || file.StartsWith("~$", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;
        var relative = Path.GetRelativePath(root, info.FullName).Replace('\\', '/');
        return _options.IgnorePatterns.Any(p => Glob(relative, p) || Glob(file, p));
    }

    private static bool Glob(string text, string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern.Trim()).Replace("\\*", ".*").Replace("\\?", ".");
        return System.Text.RegularExpressions.Regex.IsMatch(text, "^" + escaped + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private string BackupPath(string relative) => Path.Combine(_options.BackupRootPath ?? Path.Combine(AppContext.BaseDirectory, "backups"), DateTime.Now.ToString("yyyyMMdd-HHmmss"), relative);
    private static string ConflictPath(string root, string relative, string side)
    {
        var folder = Path.GetDirectoryName(Path.Combine(root, relative))!;
        var name = Path.GetFileNameWithoutExtension(relative);
        var extension = Path.GetExtension(relative);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var candidate = Path.Combine(folder, $"{name}_衝突_{side}_{stamp}{extension}");
        var i = 2;
        while (File.Exists(candidate)) candidate = Path.Combine(folder, $"{name}_衝突_{side}_{stamp}_{i++}{extension}");
        return candidate;
    }

    private void ValidatePaths()
    {
        if (string.IsNullOrWhiteSpace(_options.SourcePath) || string.IsNullOrWhiteSpace(_options.TargetPath)) throw new InvalidOperationException("請先設定來源 A 與目標 B 資料夾。");
        var a = Path.GetFullPath(_options.SourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var b = Path.GetFullPath(_options.TargetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(a) || !Directory.Exists(b)) throw new DirectoryNotFoundException("來源 A 或目標 B 資料夾不存在。");
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase) || a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("A 與 B 必須是兩個獨立且不互相包含的資料夾。");
    }

    private Dictionary<string, SyncPair> LoadState()
    {
        var path = _options.StateFilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        try { return JsonSerializer.Deserialize<Dictionary<string, SyncPair>>(File.ReadAllText(path)) ?? new(StringComparer.OrdinalIgnoreCase); }
        catch
        {
            PreserveStateDiagnostic(path);
            throw new InvalidOperationException("同步狀態檔毀損；已保留 diagnostics，為避免誤刪，請重新建立同步基準。");
        }
    }

    private static void PreserveStateDiagnostic(string statePath)
    {
        try
        {
            var diagnostics = Path.Combine(Path.GetDirectoryName(statePath)!, "diagnostics"); Directory.CreateDirectory(diagnostics);
            var destination = Path.Combine(diagnostics, $"{Path.GetFileNameWithoutExtension(statePath)}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json");
            var temporary = destination + ".tmp"; File.Copy(statePath, temporary, true); File.Move(temporary, destination, true);
        }
        catch { /* diagnostics must never weaken fail-closed validation */ }
    }

    private void SaveState()
    {
        if (string.IsNullOrWhiteSpace(_options.StateFilePath)) return;
        var a = ScanFiles(_options.SourcePath);
        var b = ScanFiles(_options.TargetPath);
        var state = a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).ToDictionary(k => k, k => new SyncPair(a.GetValueOrDefault(k)?.ToStamp(), b.GetValueOrDefault(k)?.ToStamp()), StringComparer.OrdinalIgnoreCase);
        var path = _options.StateFilePath!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }

    private sealed record FileEntry(long Length, DateTime LastWriteUtc)
    {
        public FileStamp ToStamp() => new(Length, LastWriteUtc);
    }
    public sealed record FileStamp(long Length, DateTime LastWriteUtc);
    public sealed record SyncPair(FileStamp? A, FileStamp? B);
}
