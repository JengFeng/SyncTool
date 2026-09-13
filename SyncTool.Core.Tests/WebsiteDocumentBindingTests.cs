using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class WebsiteDocumentBindingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolBindingTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Unbound_job_keeps_its_own_source_path()
    {
        var job = new SyncJobDefinition { Name = "獨立", SourcePath = "C:\\A", TargetPath = "D:\\B" };
        Assert.Equal("C:\\A", job.ResolveSourcePath());
    }

    [Fact]
    public void Bound_job_reads_source_path_from_website_binding_file()
    {
        var documents = Path.Combine(_root, "document-library");
        Directory.CreateDirectory(documents);
        var bindingPath = Path.Combine(_root, "document-library-binding.json");
        File.WriteAllText(bindingPath, System.Text.Json.JsonSerializer.Serialize(new { documentLibraryRoot = documents }));
        var job = new SyncJobDefinition { Name = "網站", SourcePath = "C:\\obsolete", TargetPath = "D:\\B", WebsiteBindingEnabled = true, WebsiteBindingConfigPath = bindingPath };
        Assert.Equal(Path.GetFullPath(documents), job.ResolveSourcePath());
    }

    [Fact]
    public void Bound_job_rejects_missing_or_invalid_binding_file()
    {
        var job = new SyncJobDefinition { Name = "網站", SourcePath = "C:\\A", TargetPath = "D:\\B", WebsiteBindingEnabled = true, WebsiteBindingConfigPath = Path.Combine(_root, "missing.json") };
        Assert.Throws<InvalidOperationException>(() => job.ResolveSourcePath());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
