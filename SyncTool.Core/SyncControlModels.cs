namespace SyncTool.Core;

public enum SyncMode
{
    TwoWay,
    AToB,
    BToA,
    Preview
}

public enum ConflictPolicy
{
    AutoKeepCopies,
    PauseForDecision
}

public static class SyncModeNames
{
    public static string Key(SyncMode mode) => mode switch
    {
        SyncMode.TwoWay => "two-way",
        SyncMode.AToB => "a-to-b",
        SyncMode.BToA => "b-to-a",
        SyncMode.Preview => "preview",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static string Display(SyncMode mode) => mode switch
    {
        SyncMode.TwoWay => "智慧雙向",
        SyncMode.AToB => "只從 A → B",
        SyncMode.BToA => "只從 B → A",
        SyncMode.Preview => "僅檢查／預覽",
        _ => mode.ToString()
    };

    public static string Description(SyncMode mode) => mode switch
    {
        SyncMode.TwoWay => "比較 A 與 B 的同步基準後雙方互相補齊；單側修改會同步到另一側，雙側同時修改時依衝突規則保留副本或等待你決定。",
        SyncMode.AToB => "以來源 A 為主端同步到目標 B；首次或切換模式時必須先預覽確認。覆寫或移除 B 的檔案前會建立備份。",
        SyncMode.BToA => "以目標 B 為主端同步回來源 A；首次或切換模式時必須先預覽確認。覆寫或移除 A 的檔案前會建立備份。",
        SyncMode.Preview => "只掃描並產生同步預覽，不寫入、不覆寫、不刪除 A 或 B 的任何檔案。",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    public static bool TryParse(string? value, out SyncMode mode)
    {
        mode = value?.Trim().ToLowerInvariant() switch
        {
            "two-way" => SyncMode.TwoWay,
            "a-to-b" => SyncMode.AToB,
            "b-to-a" => SyncMode.BToA,
            "preview" => SyncMode.Preview,
            _ => SyncMode.TwoWay
        };
        return value?.Trim().ToLowerInvariant() is "two-way" or "a-to-b" or "b-to-a" or "preview";
    }
}

public static class SyncStateLayout
{
    public static string ModeStatePath(string appRoot, string jobId, SyncMode mode) => Path.Combine(appRoot, "jobs", jobId, "state", $"{SyncModeNames.Key(mode)}.json");

    public static bool MigrateLegacyState(string appRoot, string jobId)
    {
        var jobRoot = Path.Combine(appRoot, "jobs", jobId);
        var legacy = Path.Combine(jobRoot, "state.json");
        var target = ModeStatePath(appRoot, jobId, SyncMode.TwoWay);
        if (File.Exists(target) || !File.Exists(legacy)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var staging = target + ".migrating";
        File.Copy(legacy, staging, overwrite: false);
        File.Move(staging, target, overwrite: false);
        var archived = Path.Combine(jobRoot, $"state-legacy-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json");
        File.Move(legacy, archived, overwrite: false);
        return true;
    }
}
