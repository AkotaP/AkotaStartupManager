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

        var backupDirectory = Path.Combine(paths.BackupDirectory, "StartupFolders");
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
            WasEnabled = true
        });
    }

    public Task RestoreAsync(StartupBackupRecord backup, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(backup.BackupLocation) || !File.Exists(backup.BackupLocation))
        {
            throw new FileNotFoundException("启动文件的备份不存在。", backup.BackupLocation);
        }
        if (File.Exists(backup.OriginalLocation))
        {
            throw new IOException($"原位置已存在同名文件：{backup.OriginalLocation}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backup.OriginalLocation)!);
        File.Move(backup.BackupLocation, backup.OriginalLocation);
        return Task.CompletedTask;
    }
}
