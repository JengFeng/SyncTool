namespace SyncTool.Core;

/// <summary>Separates an in-progress job from the user's selected job in the UI.</summary>
public sealed class SyncRunGuard
{
    public bool IsRunning { get; private set; }
    public string? RunningJobId { get; private set; }
    public bool CanChangeSelectedJob => !IsRunning;

    public void Begin(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId)) throw new ArgumentException("工作識別碼不可空白。", nameof(jobId));
        if (IsRunning) throw new InvalidOperationException("已有同步工作正在執行。");
        IsRunning = true;
        RunningJobId = jobId;
    }

    public void End()
    {
        IsRunning = false;
        RunningJobId = null;
    }
}
