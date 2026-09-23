using AkotaStartupManager.Core.Models;
using Microsoft.Win32;

namespace AkotaStartupManager.Infrastructure.Windows;

/// <summary>
/// 判断恢复一条备份是否需要管理员权限。
/// 新建的备份记录会直接写入该信息；旧记录没有记录时按原始位置推导。
/// </summary>
public static class StartupElevationResolver
{
    public static bool RequiresElevation(StartupBackupRecord backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (backup.RequiresElevation is { } recorded)
        {
            return recorded;
        }

        return backup.SourceType switch
        {
            StartupSourceType.Registry => RegistryRequiresElevation(backup),
            StartupSourceType.StartupFolder => IsCommonStartupFolder(backup.OriginalLocation),
            StartupSourceType.ScheduledTask => IsSystemTask(backup),
            _ => false
        };
    }

    private static bool RegistryRequiresElevation(StartupBackupRecord backup)
    {
        if (string.IsNullOrWhiteSpace(backup.OriginalLocation))
        {
            return false;
        }

        try
        {
            return RegistryStartupProvider.ParseLocation(backup.OriginalLocation).Hive == RegistryHive.LocalMachine;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsCommonStartupFolder(string location)
    {
        var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(commonStartup))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(location) ?? string.Empty;
        return directory.TrimEnd(Path.DirectorySeparatorChar)
            .Equals(commonStartup.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemTask(StartupBackupRecord backup) =>
        backup.Metadata?.GetValueOrDefault("TaskPath", "\\")
            .StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase) == true;
}
