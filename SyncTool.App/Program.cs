using System.Text.Json;
using SyncTool.Core;

namespace SyncTool.App;

internal static class Program
{
    private const string SemaphoreName = "Global\\SyncTool_SyncSemaphore";

    [STAThread]
    static async Task Main(string[] args)
    {
        var root = AppContext.BaseDirectory;
        var configPath = Path.Combine(root, "config.json");
        var runOnce = args.Contains("--run-once", StringComparer.OrdinalIgnoreCase);
        var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var configured = args.FirstOrDefault(a => a.StartsWith("--config=", StringComparison.OrdinalIgnoreCase));
        var jobArg = args.FirstOrDefault(a => a.StartsWith("--job=", StringComparison.OrdinalIgnoreCase));
        var modeArg = args.FirstOrDefault(a => a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase));
        var confirmArg = args.FirstOrDefault(a => a.StartsWith("--confirm-preview=", StringComparison.OrdinalIgnoreCase));
        if (configured is not null) configPath = configured["--config=".Length..].Trim('"');
        var jobSelector = jobArg is null ? null : jobArg["--job=".Length..].Trim('"');
        var modeSelector = modeArg is null ? null : modeArg["--mode=".Length..].Trim('"');
        var previewId = confirmArg is null ? null : confirmArg["--confirm-preview=".Length..].Trim('"');

        if (runOnce || dryRun || args.Length > 0)
        {
            Environment.ExitCode = await RunCliAsync(configPath, runOnce, dryRun, jobSelector, modeSelector, previewId, root);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(configPath));
    }

    private static async Task<int> RunCliAsync(string configPath, bool runOnce, bool dryRun, string? jobSelector, string? modeSelector, string? previewId, string root)
    {
        if (runOnce == dryRun || string.IsNullOrWhiteSpace(jobSelector))
        {
            var message = runOnce == dryRun ? "請指定 --run-once 或 --dry-run，且不可同時使用。" : "多組同步模式必須以 --job=\"工作名稱或ID\" 指定工作。";
            Console.Out.Write(JsonSerializer.Serialize(Failure(message, root))); return 2;
        }
        if (modeSelector is not null && !SyncModeNames.TryParse(modeSelector, out _))
        {
            Console.Out.Write(JsonSerializer.Serialize(Failure("--mode 必須是 two-way、a-to-b、b-to-a 或 preview。", root))); return 2;
        }
        using var semaphore = new Semaphore(1, 1, SemaphoreName);
        if (!semaphore.WaitOne(TimeSpan.FromSeconds(60))) { Console.Out.Write(JsonSerializer.Serialize(Failure("同步佇列忙碌，等待 60 秒後逾時。", root))); return 1; }
        try
        {
            var config = AppConfig.Load(configPath);
            var job = config.GetJob(jobSelector);
            SyncStateLayout.MigrateLegacyState(root, job.Id);
            var mode = modeSelector is null ? job.DefaultMode : ParseMode(modeSelector);
            var result = await new SyncEngine(job.ToOptions(root, mode, previewId)).RunAsync(dryRun);
            job.LastSyncAt = DateTimeOffset.Now;
            var logPath = WriteLog(root, job, result);
            config.Save(configPath);
            Console.Out.Write(JsonSerializer.Serialize(new { status = result.Success ? "success" : "fail", jobId = job.Id, jobName = job.Name, mode = SyncModeNames.Key(mode), previewId = result.PreviewId, previewExpiresAt = result.PreviewExpiresAt, syncTime = result.SyncTime, addCount = result.AddCount, updateCount = result.UpdateCount, deleteCount = result.DeleteCount, conflictCount = result.ConflictCount, errorMsg = result.ErrorMessage, logPath }));
            return result.Success ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Console.Out.Write(JsonSerializer.Serialize(Failure(ex.Message, root))); return 1; }
        finally { semaphore.Release(); }
    }

    private static SyncMode ParseMode(string value)
    {
        if (SyncModeNames.TryParse(value, out var mode)) return mode;
        throw new ArgumentException("無效的同步模式。");
    }

    private static object Failure(string message, string root) => new { status = "fail", syncTime = DateTime.Now, addCount = 0, updateCount = 0, deleteCount = 0, conflictCount = 0, errorMsg = message, logPath = Path.Combine(root, "jobs") };

    internal static string WriteLog(string root, SyncJobDefinition job, SyncResult result)
    {
        var paths = SyncLogStorage.GetPaths(root, job.Id, DateTime.Now);
        var lines = new[] { $"[{result.SyncTime:O}] {job.Name} {(result.Success ? "SUCCESS" : "FAIL")} +{result.AddCount} ~{result.UpdateCount} -{result.DeleteCount} !{result.ConflictCount} {result.ErrorMessage}" }.Concat(result.Events).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(paths.JobLogPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RootLogPath)!);
        File.AppendAllLines(paths.JobLogPath, lines);
        File.AppendAllLines(paths.RootLogPath, lines);
        return paths.JobLogPath;
    }
}
