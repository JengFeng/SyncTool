namespace SyncTool.Core.Tests;

public sealed class VersionInfoUiContractTests
{
    [Fact]
    public void Main_form_exposes_build_version_git_commit_and_commit_time()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SyncTool.App", "MainForm.cs")));
        Assert.Contains("版本資訊", source);
        Assert.Contains("Git Commit", source);
        Assert.Contains("最新 Commit 時間", source);
        Assert.Contains("BuildVersionInfo", source);
    }
}
