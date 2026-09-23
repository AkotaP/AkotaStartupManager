using System.IO;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Infrastructure.Windows;

namespace AkotaStartupManager.App.ViewModels;

/// <summary>
/// 备份列表窗口的展示行。需要查询磁盘与注册表的字段在这里预先算好，
/// 列表本身只做绑定，不再触发额外的 I/O。
/// </summary>
public sealed class BackupRow(StartupBackupRecord record, BackupAvailability availability)
{
    public StartupBackupRecord Record { get; } = record;
    public BackupAvailability Availability { get; } = availability;

    public string Name => Record.Name;
    public string SourceText => Record.SourceType switch
    {
        StartupSourceType.Registry => "注册表",
        StartupSourceType.StartupFolder => "启动文件夹",
        StartupSourceType.ScheduledTask => "计划任务",
        _ => Record.SourceType.ToString()
    };

    public string StatusText => Availability switch
    {
        BackupAvailability.Ready => "可恢复",
        BackupAvailability.ValueAlreadyExists => "原位置已存在同名项",
        BackupAvailability.BackupFileMissing => "备份文件已丢失",
        _ => "记录不完整"
    };

    public string LocationText => Record.OriginalLocation;
    public string CreatedAtText => Record.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string ElevationText => StartupElevationResolver.RequiresElevation(Record) ? "需要管理员" : "普通权限";
    public bool CanRestore => Availability == BackupAvailability.Ready;
}

/// <summary>
/// 磁盘上存在、但没有任何备份记录引用的备份文件。
/// 配置损坏回退到 config.previous.json 时会产生这类文件，界面需要能把它们捞回来。
/// </summary>
public sealed class OrphanBackupRow(string backupPath, string originalFileName, long sizeInBytes)
{
    public string BackupPath { get; } = backupPath;
    public string FileName { get; } = Path.GetFileName(backupPath);
    public string OriginalFileName { get; } = originalFileName;
    public string SizeText { get; } = sizeInBytes < 1024 ? $"{sizeInBytes} B" : $"{sizeInBytes / 1024.0:0.#} KB";
}
