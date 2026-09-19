using System.Diagnostics;
using System.Reflection;

namespace SyncTool.App;

internal sealed record BuildVersionInfo(string AppVersion, string GitCommit, string GitVersion, string CommitTime, string Repository, string WorkingTree)
{
    public static BuildVersionInfo Load()
    {
        var appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "未知";
        var root = FindGitRoot(AppContext.BaseDirectory);
        if (root is null) return new(appVersion, "無法取得", "無法取得", "無法取得", "未找到 Git repository", "無法確認");

        var commit = Git(root, "rev-parse --short=12 HEAD");
        var version = Git(root, "describe --tags --always --dirty");
        var time = Git(root, "log -1 --format=%cI");
        var repository = Git(root, "remote get-url origin");
        var dirty = Git(root, "status --porcelain");
        return new(appVersion, EmptyAsUnknown(commit), EmptyAsUnknown(version), EmptyAsUnknown(time), EmptyAsUnknown(repository), string.IsNullOrWhiteSpace(dirty) ? "工作目錄乾淨" : "本機有未提交變更");
    }

    private static string? FindGitRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
        return null;
    }

    private static string Git(string root, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", $"-C \"{root}\" {arguments}") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process is null) return string.Empty;
            if (!process.WaitForExit(2000)) { process.Kill(true); return string.Empty; }
            return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd().Trim() : string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string EmptyAsUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "無法取得" : value;
}
