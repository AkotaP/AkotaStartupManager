using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.Infrastructure.Windows;

/// <summary>
/// 解析启动文件夹备份文件的实际位置。
/// 备份记录优先使用相对程序目录的路径，这样整个程序目录被移动后仍能找回备份；
/// 绝对路径与按文件名匹配仅用于兼容旧记录。
/// </summary>
public static class BackupPathResolver
{
    public const string StartupFolderBackupSubdirectory = "StartupFolders";

    public static string? Resolve(StartupBackupRecord backup, IPortablePathService paths)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(paths);

        return ResolveRelative(backup, paths)
               ?? ResolveAbsolute(backup)
               ?? ResolveByFileName(backup, paths);
    }

    private static string? ResolveRelative(StartupBackupRecord backup, IPortablePathService paths)
    {
        if (string.IsNullOrWhiteSpace(backup.RelativeBackupPath))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(paths.BaseDirectory, backup.RelativeBackupPath));
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? ResolveAbsolute(StartupBackupRecord backup) =>
        !string.IsNullOrWhiteSpace(backup.BackupLocation) && File.Exists(backup.BackupLocation)
            ? backup.BackupLocation
            : null;

    /// <summary>
    /// 兜底：备份文件名形如 {32 位 guid}-{原始文件名}，在备份目录里按原始文件名找回。
    /// 同时覆盖“旧版本只记录了绝对路径”和“程序目录已经被移动”两种情况。
    /// </summary>
    private static string? ResolveByFileName(StartupBackupRecord backup, IPortablePathService paths)
    {
        var originalFileName = Path.GetFileName(backup.OriginalLocation);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return null;
        }

        var directory = Path.Combine(paths.BackupDirectory, StartupFolderBackupSubdirectory);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory)
            .Where(x => IsBackupOf(Path.GetFileName(x), originalFileName))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool IsBackupOf(string candidateFileName, string originalFileName)
    {
        var suffix = $"-{originalFileName}";
        if (!candidateFileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 前缀必须是 32 位十六进制的 guid，避免把别的启动项备份误认成本条记录的备份。
        var prefix = candidateFileName[..^suffix.Length];
        return prefix.Length == 32 && prefix.All(Uri.IsHexDigit);
    }
}
