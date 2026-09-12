using System.Text.Json;
using System.Text.Json.Serialization;
using SyncTool.Core;

namespace SyncTool.App;

public sealed class AppConfig
{
    // Legacy fields remain solely to migrate pre-multi-job config.json files safely.
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string SyncFrequency { get; set; } = "Manual";
    public bool AutoStart { get; set; }
    public bool DeleteProtectionEnabled { get; set; } = true;
    public bool ConflictCopiesEnabled { get; set; } = true;
    public bool LockRetryEnabled { get; set; } = true;
    public List<string> IgnorePatterns { get; set; } = [];

    public List<SyncJobDefinition> Jobs { get; set; } = [];

    public static AppConfig Load(string path)
    {
        var config = File.Exists(path) ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path)) ?? new AppConfig() : new AppConfig();
        if (config.Jobs.Count == 0 && !string.IsNullOrWhiteSpace(config.SourcePath) && !string.IsNullOrWhiteSpace(config.TargetPath))
        {
            config.Jobs.Add(new SyncJobDefinition
            {
                Name = "預設同步工作",
                SourcePath = config.SourcePath,
                TargetPath = config.TargetPath,
                SyncFrequency = config.SyncFrequency,
                IsEnabled = false,
                DeleteProtectionEnabled = config.DeleteProtectionEnabled,
                ConflictCopiesEnabled = config.ConflictCopiesEnabled,
                LockRetryEnabled = config.LockRetryEnabled,
                IgnorePatterns = config.IgnorePatterns.ToList()
            });
        }
        return config;
    }

    public void Save(string path)
    {
        SyncJobDefinition.ValidateSet(Jobs);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public SyncJobDefinition GetJob(string selector)
    {
        var job = Jobs.FirstOrDefault(j => !j.IsArchived && (j.Id.Equals(selector, StringComparison.OrdinalIgnoreCase) || j.Name.Equals(selector, StringComparison.OrdinalIgnoreCase)));
        return job ?? throw new InvalidOperationException($"找不到同步工作：{selector}");
    }

    [JsonIgnore]
    public IEnumerable<SyncJobDefinition> ActiveJobs => Jobs.Where(j => !j.IsArchived);
}
