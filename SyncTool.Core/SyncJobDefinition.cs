namespace SyncTool.Core;

public sealed class SyncJobDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "未命名同步工作";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string SyncFrequency { get; set; } = "Manual";
    public bool IsEnabled { get; set; }
    public bool DeleteProtectionEnabled { get; set; } = true;
    public bool ConflictCopiesEnabled { get; set; } = true;
    public bool LockRetryEnabled { get; set; } = true;
    public SyncMode DefaultMode { get; set; } = SyncMode.TwoWay;
    public bool PreserveTargetExtras { get; set; }
    public ConflictPolicy ConflictPolicy { get; set; } = ConflictPolicy.AutoKeepCopies;
    public int RiskOperationThreshold { get; set; } = 100;
    public long RiskBytesThreshold { get; set; } = 10L * 1024 * 1024 * 1024;
    public int BackupRetentionDays { get; set; } = 90;
    public bool DirectionalModeApproved { get; set; }
    public List<string> IgnorePatterns { get; set; } = [];
    public DateTimeOffset? LastSyncAt { get; set; }
    public DateTimeOffset? NextSyncAt { get; set; }
    public bool IsArchived { get; set; }

    public SyncOptions ToOptions(string appRoot, SyncMode? mode = null, string? requiredPreviewId = null)
    {
        var selectedMode = mode ?? DefaultMode;
        return new()
        {
            SourcePath = SourcePath,
            TargetPath = TargetPath,
            DeleteProtectionEnabled = DeleteProtectionEnabled,
            ConflictCopiesEnabled = ConflictCopiesEnabled,
            LockRetryEnabled = LockRetryEnabled,
            Mode = selectedMode,
            PreserveTargetExtras = PreserveTargetExtras,
            ConflictPolicy = ConflictPolicy,
            RiskOperationThreshold = RiskOperationThreshold,
            RiskBytesThreshold = RiskBytesThreshold,
            IgnorePatterns = IgnorePatterns,
            BackupRootPath = Path.Combine(appRoot, "jobs", Id, "backups"),
            StateFilePath = SyncStateLayout.ModeStatePath(appRoot, Id, selectedMode == SyncMode.Preview ? DefaultMode : selectedMode),
            PreviewRootPath = Path.Combine(appRoot, "jobs", Id, "previews"),
            JobId = Id,
            RequiredPreviewId = requiredPreviewId,
            PendingConflictPath = Path.Combine(appRoot, "jobs", Id, "pending-conflicts.json")
        };
    }

    public static void ValidateSet(IEnumerable<SyncJobDefinition> jobs)
    {
        var active = jobs.Where(j => !j.IsArchived).ToList();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in active)
        {
            if (string.IsNullOrWhiteSpace(job.Id) || !ids.Add(job.Id)) throw new InvalidOperationException("同步工作 ID 不可重複。");
            if (string.IsNullOrWhiteSpace(job.Name) || !names.Add(job.Name.Trim())) throw new InvalidOperationException("同步工作名稱不可重複。");
            if (string.IsNullOrWhiteSpace(job.SourcePath) || string.IsNullOrWhiteSpace(job.TargetPath)) throw new InvalidOperationException($"工作「{job.Name}」必須指定 A 與 B 路徑。");
            var source = Path.GetFullPath(job.SourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.GetFullPath(job.TargetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (source.Equals(target, StringComparison.OrdinalIgnoreCase) || source.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || target.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"工作「{job.Name}」的 A／B 必須是獨立資料夾。");
            if (!endpoints.Add(source) || !endpoints.Add(target)) throw new InvalidOperationException("不同同步工作不可重複使用相同的來源或目標路徑。");
        }
    }
}
