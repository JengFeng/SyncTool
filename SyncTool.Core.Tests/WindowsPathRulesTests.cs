using SyncTool.Core;

namespace SyncTool.Core.Tests;

public sealed class WindowsPathRulesTests
{
    [Theory]
    [InlineData("nul")]
    [InlineData("NUL.txt")]
    [InlineData("con")]
    [InlineData("AUX.log")]
    [InlineData("COM1")]
    [InlineData("lpt9.prn")]
    public void IsReservedDeviceName_recognizes_windows_device_names(string name)
        => Assert.True(WindowsPathRules.IsReservedDeviceName(name));

    [Theory]
    [InlineData("report.txt")]
    [InlineData("null-data.txt")]
    [InlineData("com10.txt")]
    public void IsReservedDeviceName_does_not_reject_normal_names(string name)
        => Assert.False(WindowsPathRules.IsReservedDeviceName(name));
}
