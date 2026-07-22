using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Core.Interfaces;

public interface IConditionChecker
{
    Type ConditionType { get; }
    Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken);
}

public interface IConditionEvaluator
{
    Task<ConditionResult> EvaluateAsync(StartupCondition condition, CancellationToken cancellationToken);
}

public interface IProcessLauncher
{
    bool IsRunning(string processName);
    Task<LaunchResult> LaunchAsync(ManagedStartupEntry entry, CancellationToken cancellationToken);
}

public interface IConfigurationRepository
{
    Task<ApplicationConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ApplicationConfiguration configuration, CancellationToken cancellationToken = default);
}

public interface IStartupProvider
{
    StartupSourceType SourceType { get; }
    Task<IReadOnlyList<StartupItem>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<StartupBackupRecord> DisableAsync(StartupItem item, CancellationToken cancellationToken = default);
    Task RestoreAsync(StartupBackupRecord backup, CancellationToken cancellationToken = default);
}

public interface IAppLogger
{
    void Information(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
}

public interface IPortablePathService
{
    string BaseDirectory { get; }
    string DataDirectory { get; }
    string BackupDirectory { get; }
    string LogsDirectory { get; }
    void EnsureWritable();
}
