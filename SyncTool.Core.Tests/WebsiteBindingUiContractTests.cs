namespace SyncTool.Core.Tests;

public sealed class WebsiteBindingUiContractTests
{
    [Fact]
    public void Bound_job_editor_refreshes_source_when_binding_file_changes_and_hides_duplicate_source_picker()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SyncTool.App", "MainForm.cs")));
        Assert.Contains("bindingPath.TextChanged", source);
        Assert.Contains("sourcePicker.Visible = !binding.Checked", source);
        Assert.Contains("變更網站唯一文件庫根…", source);
    }
}
