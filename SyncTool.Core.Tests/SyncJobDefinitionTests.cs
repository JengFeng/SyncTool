using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SyncJobDefinitionTests
{
    [Fact]
    public void ValidateSet_rejects_duplicate_source_or_target_paths()
    {
        var jobs = new[]
        {
            new SyncJobDefinition { Id = "a", Name = "網站", SourcePath = "C:\\A", TargetPath = "D:\\B" },
            new SyncJobDefinition { Id = "b", Name = "文件", SourcePath = "C:\\A", TargetPath = "D:\\C" }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => SyncJobDefinition.ValidateSet(jobs));
        Assert.Contains("重複", exception.Message);
    }

    [Fact]
    public void ToOptions_uses_requested_mode_state_and_preview_root()
    {
        var job = new SyncJobDefinition { Id = "job-123", Name = "網站", SourcePath = "C:\\A", TargetPath = "D:\\B" };
        var options = job.ToOptions("C:\\SyncTool", SyncMode.AToB, "preview-1");
        Assert.Equal("C:\\SyncTool\\jobs\\job-123\\state\\a-to-b.json", options.StateFilePath);
        Assert.Equal("C:\\SyncTool\\jobs\\job-123\\previews", options.PreviewRootPath);
        Assert.Equal("preview-1", options.RequiredPreviewId);
    }
}
