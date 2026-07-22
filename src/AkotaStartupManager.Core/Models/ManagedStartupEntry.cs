using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Core.Models;

public sealed class ManagedStartupEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public ConditionGroup Conditions { get; set; } = new();
    public int PollIntervalSeconds { get; set; } = 2;
    public int RequiredConsecutiveSuccesses { get; set; } = 3;
    public int DelayAfterReadySeconds { get; set; }
    public int StartupVerificationSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;
    public string? OriginalStartupItemId { get; set; }

    public string ProcessName => Path.GetFileNameWithoutExtension(ExecutablePath);

    public ManagedStartupEntry DeepClone() => new()
    {
        Id = Id,
        Name = Name,
        ExecutablePath = ExecutablePath,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        IsEnabled = IsEnabled,
        Conditions = (ConditionGroup)Conditions.DeepClone(),
        PollIntervalSeconds = PollIntervalSeconds,
        RequiredConsecutiveSuccesses = RequiredConsecutiveSuccesses,
        DelayAfterReadySeconds = DelayAfterReadySeconds,
        StartupVerificationSeconds = StartupVerificationSeconds,
        MaxRetries = MaxRetries,
        OriginalStartupItemId = OriginalStartupItemId
    };
}

public sealed class ApplicationConfiguration
{
    public int SchemaVersion { get; set; } = 1;
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public List<ManagedStartupEntry> Entries { get; set; } = [];
    public List<StartupBackupRecord> Backups { get; set; } = [];
}

public sealed class StartupBackupRecord
{
    public string StartupItemId { get; set; } = string.Empty;
    public StartupSourceType SourceType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OriginalLocation { get; set; } = string.Empty;
    public string? BackupLocation { get; set; }
    public string? OriginalValue { get; set; }
    public int? OriginalRegistryValueKind { get; set; }
    public bool WasEnabled { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
