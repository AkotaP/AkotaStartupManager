using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Infrastructure.Windows;

namespace AkotaStartupManager.Infrastructure.Tests.Windows;

public sealed class BackupPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AkotaTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_UsesRelativePath()
    {
        var paths = new TestPaths(_root);
        paths.EnsureWritable();
        var backupFile = CreateBackupFile(paths, "app.lnk");
        var backup = CreateBackup(backupFile, Path.GetRelativePath(paths.BaseDirectory, backupFile));

        Assert.Equal(backupFile, BackupPathResolver.Resolve(backup, paths));
    }

    [Fact]
    public void Resolve_FallsBackToAbsolutePathForLegacyRecord()
    {
        var paths = new TestPaths(_root);
        paths.EnsureWritable();
        var backupFile = CreateBackupFile(paths, "app.lnk");
        var legacy = CreateBackup(backupFile, relativePath: null);

        Assert.Equal(backupFile, BackupPathResolver.Resolve(legacy, paths));
    }

    /// <summary>
    /// 便携目录整体移动后，记录里的相对路径和绝对路径同时失效，
    /// 必须还能按“{guid}-{原始文件名}”在当前的备份目录里找回。
    /// </summary>
    [Fact]
    public void Resolve_FindsBackupAfterPortableDirectoryIsMoved()
    {
        // 搬迁前：备份写在旧的程序目录里，记录保存了当时的绝对路径与相对路径。
        var oldPaths = new TestPaths(Path.Combine(_root, "before-move"));
        oldPaths.EnsureWritable();
        var backupFile = CreateBackupFile(oldPaths, "app.lnk");
        var backup = CreateBackup(backupFile, Path.GetRelativePath(oldPaths.BaseDirectory, backupFile));

        // 搬迁后：整个程序目录换了位置，两条旧路径都失效，备份文件跟着到了新目录。
        var newPaths = new TestPaths(Path.Combine(_root, "after-move"));
        var expected = Path.Combine(newPaths.BackupDirectory, BackupPathResolver.StartupFolderBackupSubdirectory,
            Path.GetFileName(backupFile));
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.Move(backupFile, expected);

        Assert.Equal(expected, BackupPathResolver.Resolve(backup, newPaths));
    }

    [Fact]
    public void Resolve_ReturnsNullWhenNothingMatches()
    {
        var paths = new TestPaths(_root);
        paths.EnsureWritable();
        var backup = new StartupBackupRecord
        {
            SourceType = StartupSourceType.StartupFolder,
            Name = "app",
            OriginalLocation = @"D:\Startup\app.lnk",
            BackupLocation = @"D:\gone\app.lnk"
        };

        Assert.Null(BackupPathResolver.Resolve(backup, paths));
    }

    /// <summary>文件名前缀不是 32 位 guid 时不能当成这条记录的备份，否则会恢复错文件。</summary>
    [Fact]
    public void Resolve_DoesNotMatchFileWithoutGuidPrefix()
    {
        var paths = new TestPaths(_root);
        paths.EnsureWritable();
        var directory = Path.Combine(paths.BackupDirectory, BackupPathResolver.StartupFolderBackupSubdirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "leftover-app.lnk"), "probe");

        var backup = new StartupBackupRecord
        {
            SourceType = StartupSourceType.StartupFolder,
            Name = "app",
            OriginalLocation = @"D:\Startup\app.lnk"
        };

        Assert.Null(BackupPathResolver.Resolve(backup, paths));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static StartupBackupRecord CreateBackup(string backupFile, string? relativePath) => new()
    {
        SourceType = StartupSourceType.StartupFolder,
        Name = "app",
        OriginalLocation = @"D:\Startup\app.lnk",
        BackupLocation = backupFile,
        RelativeBackupPath = relativePath
    };

    private static string CreateBackupFile(TestPaths paths, string originalFileName)
    {
        var directory = Path.Combine(paths.BackupDirectory, BackupPathResolver.StartupFolderBackupSubdirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}-{originalFileName}");
        File.WriteAllText(path, "probe");
        return path;
    }
}
