using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class WebsiteDocumentBindingWriteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolBindingWriteTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_persists_a_binding_that_can_be_read_back()
    {
        var documents = Path.Combine(_root, "document-library");
        Directory.CreateDirectory(documents);
        var bindingPath = Path.Combine(_root, "settings", "document-library-binding.json");

        WebsiteDocumentBindingStore.Write(bindingPath, documents);

        Assert.Equal(Path.GetFullPath(documents), WebsiteDocumentBindingStore.Read(bindingPath).DocumentLibraryRoot);
        var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(bindingPath));
        Assert.Equal(Path.GetFullPath(documents), json.RootElement.GetProperty("documentLibraryRoot").GetString());
    }

    [Fact]
    public void Write_rejects_a_missing_document_root_without_creating_a_config()
    {
        var bindingPath = Path.Combine(_root, "settings", "document-library-binding.json");
        Assert.Throws<InvalidOperationException>(() => WebsiteDocumentBindingStore.Write(bindingPath, Path.Combine(_root, "missing")));
        Assert.False(File.Exists(bindingPath));
    }

    [Fact]
    public void Binding_store_rejects_any_filename_other_than_document_library_binding_json()
    {
        var documents = Path.Combine(_root, "document-library");
        Directory.CreateDirectory(documents);
        var wrongPath = Path.Combine(_root, "bridge_settings.json");
        File.WriteAllText(wrongPath, System.Text.Json.JsonSerializer.Serialize(new { documentLibraryRoot = documents }));

        Assert.Throws<InvalidOperationException>(() => WebsiteDocumentBindingStore.Read(wrongPath));
        Assert.Throws<InvalidOperationException>(() => WebsiteDocumentBindingStore.Write(wrongPath, documents));
        Assert.Contains("documentLibraryRoot", File.ReadAllText(wrongPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
