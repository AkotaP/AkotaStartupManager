using AkotaStartupManager.Core.Interfaces;

namespace AkotaStartupManager.Infrastructure.Tests;

/// <summary>把便携目录指向临时目录，避免测试碰到真实的程序目录。</summary>
public sealed class TestPaths(string root) : IPortablePathService
{
    public string BaseDirectory => root;
    public string DataDirectory => Path.Combine(root, "Data");
    public string BackupDirectory => Path.Combine(DataDirectory, "Backups");
    public string LogsDirectory => Path.Combine(root, "Logs");

    public void EnsureWritable()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
