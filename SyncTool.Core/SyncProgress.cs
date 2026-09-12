namespace SyncTool.Core;

public enum SyncStage
{
    Scanning,
    Syncing,
    Completed,
    Failed
}

public sealed record SyncProgress(SyncStage Stage, int Processed, int Total, string CurrentPath, string Message);
