using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class SyncControlModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SyncToolTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(SyncMode.TwoWay, "two-way.json")]
    [InlineData(SyncMode.AToB, "a-to-b.json")]
    [InlineData(SyncMode.BToA, "b-to-a.json")]
    public void StatePath_is_isolated_by_mode(SyncMode mode, string filename)
    {
        Assert.Equal(Path.Combine(_root, "jobs", "job-1", "state", filename), SyncStateLayout.ModeStatePath(_root, "job-1", mode));
    }

    [Fact]
    public void MigrateLegacyState_preserves_original_and_creates_two_way_state()
    {
        var legacy = Path.Combine(_root, "jobs", "job-1", "state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, "{\"one.txt\":{\"A\":null,\"B\":null}}");

        var migrated = SyncStateLayout.MigrateLegacyState(_root, "job-1");

        Assert.True(migrated);
        Assert.True(File.Exists(SyncStateLayout.ModeStatePath(_root, "job-1", SyncMode.TwoWay)));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "jobs", "job-1"), "state-legacy-*.json"));
        Assert.False(File.Exists(legacy));
    }

    [Theory]
    [InlineData(SyncMode.TwoWay, "雙方")]
    [InlineData(SyncMode.AToB, "A")]
    [InlineData(SyncMode.BToA, "B")]
    [InlineData(SyncMode.Preview, "不寫入")]
    public void Every_sync_mode_has_a_clear_chinese_description(SyncMode mode, string expectedPhrase)
    {
        var description = SyncModeNames.Description(mode);
        Assert.Contains(expectedPhrase, description); Assert.True(description.Length > 20);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
