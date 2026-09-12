namespace SyncTool.Core;

public static class WindowsPathRules
{
    private static readonly HashSet<string> ReservedBases = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CLOCK$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static bool IsReservedDeviceName(string fileName)
    {
        var leaf = Path.GetFileName(fileName).TrimEnd(' ', '.');
        if (leaf.Length == 0) return false;
        var baseName = leaf.Split('.', 2)[0];
        return ReservedBases.Contains(baseName);
    }
}
