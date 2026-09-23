using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using Microsoft.Win32;

namespace AkotaStartupManager.Infrastructure.Windows;

public enum BackupAvailability
{
    /// <summary>可以恢复。</summary>
    Ready,

    /// <summary>原位置已经存在同名的注册表值或文件，恢复会被拒绝以免覆盖。</summary>
    ValueAlreadyExists,

    /// <summary>备份文件已不在磁盘上。</summary>
    BackupFileMissing,

    /// <summary>记录本身不完整（旧版本记录或配置被改动过）。</summary>
    RecordIncomplete
}

/// <summary>
/// 在界面上标注每条备份是否还恢复得回来，避免用户点了才发现不行。
/// 只做本地判断，不启动额外进程。
/// </summary>
public static class StartupBackupInspector
{
    public static BackupAvailability Inspect(StartupBackupRecord backup, IPortablePathService paths)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(paths);

        return backup.SourceType switch
        {
            StartupSourceType.Registry => InspectRegistry(backup),
            StartupSourceType.StartupFolder => InspectStartupFolder(backup, paths),
            StartupSourceType.ScheduledTask => InspectScheduledTask(backup),
            _ => BackupAvailability.RecordIncomplete
        };
    }

    private static BackupAvailability InspectRegistry(StartupBackupRecord backup)
    {
        if (string.IsNullOrWhiteSpace(backup.OriginalLocation) || !RegistryValueSerialization.CanDecode(backup))
        {
            return BackupAvailability.RecordIncomplete;
        }

        try
        {
            var location = RegistryStartupProvider.ParseLocation(backup.OriginalLocation);
            using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var key = baseKey.OpenSubKey(location.Path, writable: false);
            return key?.GetValue(backup.Name) is null
                ? BackupAvailability.Ready
                : BackupAvailability.ValueAlreadyExists;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
                                       or System.Security.SecurityException)
        {
            return BackupAvailability.RecordIncomplete;
        }
    }

    private static BackupAvailability InspectStartupFolder(StartupBackupRecord backup, IPortablePathService paths)
    {
        if (string.IsNullOrWhiteSpace(backup.OriginalLocation))
        {
            return BackupAvailability.RecordIncomplete;
        }

        if (BackupPathResolver.Resolve(backup, paths) is null)
        {
            return BackupAvailability.BackupFileMissing;
        }

        return File.Exists(backup.OriginalLocation)
            ? BackupAvailability.ValueAlreadyExists
            : BackupAvailability.Ready;
    }

    private static BackupAvailability InspectScheduledTask(StartupBackupRecord backup)
    {
        // 每条记录都起一次 PowerShell 查询状态太慢，这里只判断记录是否完整。
        var taskName = backup.Metadata?.GetValueOrDefault("TaskName");
        return string.IsNullOrWhiteSpace(taskName) ? BackupAvailability.RecordIncomplete : BackupAvailability.Ready;
    }
}
