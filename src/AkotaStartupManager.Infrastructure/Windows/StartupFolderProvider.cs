using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.Infrastructure.Windows;

public sealed class StartupFolderProvider(IPortablePathService paths) : IStartupProvider
{
    public StartupSourceType SourceType => StartupSourceType.StartupFolder;

    public Task<IReadOnlyList<StartupItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var folders = new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupScope.CurrentUser, false),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupScope.AllUsers, true)
        };
        var items = new List<StartupItem>();
        foreach (var (folder, scope, elevation) in folders)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.Add(new StartupItem(
                    $"startupFolder:{file}", Path.GetFileNameWithoutExtension(file), file, string.Empty,
                    folder, SourceType, scope, true, elevation, file));
            }
        }

        return Task.FromResult<IReadOnlyList<StartupItem>>(items);
    }

    public Task<StartupBackupRecord> DisableAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(item.Location))
        {
            throw new FileNotFoundException("启动文件已被外部删除。", item.Location);
        }

        var backupDirectory = Path.Combine(paths.BackupDirectory, BackupPathResolver.StartupFolderBackupSubdirectory);
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, $"{Guid.NewGuid():N}-{Path.GetFileName(item.Location)}");
        File.Move(item.Location, backupPath);
        return Task.FromResult(new StartupBackupRecord
        {
            StartupItemId = item.Id,
            SourceType = SourceType,
            Name = item.Name,
            OriginalLocation = item.Location,
            BackupLocation = backupPath,
            RelativeBackupPath = Path.GetRelativePath(paths.BaseDirectory, backupPath),
            RequiresElevation = item.RequiresElevation,
            WasEnabled = true
        });
    }

    public Task RestoreAsync(StartupBackupRecord backup, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var backupPath = BackupPathResolver.Resolve(backup, paths)
            ?? throw new FileNotFoundException(
                $"备份“{backup.Name}”的文件不存在，可能已被移动或删除。",
                backup.BackupLocation ?? backup.RelativeBackupPath ?? string.Empty);
        if (File.Exists(backup.OriginalLocation))
        {
            throw new IOException($"原位置已存在同名文件：{backup.OriginalLocation}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backup.OriginalLocation)!);
        File.Move(backupPath, backup.OriginalLocation);
        return Task.CompletedTask;
    }
}
