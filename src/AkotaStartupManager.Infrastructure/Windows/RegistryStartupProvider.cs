using Microsoft.Win32;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.Infrastructure.Windows;

public sealed class RegistryStartupProvider : IStartupProvider
{
    private static readonly (RegistryHive Hive, RegistryView View, string Path, StartupScope Scope, bool RequiresElevation)[] Locations =
    [
        (RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupScope.CurrentUser, false),
        (RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupScope.CurrentUser, false),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupScope.AllUsers, true),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupScope.AllUsers, true),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupScope.AllUsers, true),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", StartupScope.AllUsers, true)
    ];

    public StartupSourceType SourceType => StartupSourceType.Registry;

    public Task<IReadOnlyList<StartupItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<StartupItem>();
        foreach (var location in Locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var key = baseKey.OpenSubKey(location.Path, writable: false);
            if (key is null)
            {
                continue;
            }

            foreach (var name in key.GetValueNames())
            {
                var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty;
                var (command, arguments) = WindowsCommandLine.Split(raw);
                var locationName = $"{location.Hive}|{location.View}|{location.Path}";
                items.Add(new StartupItem(
                    $"registry:{locationName}:{name}", name, command, arguments,
                    Path.GetDirectoryName(command) ?? string.Empty, SourceType, location.Scope, true,
                    location.RequiresElevation, locationName,
                    new Dictionary<string, string>
                    {
                        ["ValueName"] = name,
                        ["ValueKind"] = ((int)key.GetValueKind(name)).ToString()
                    }));
            }
        }

        return Task.FromResult<IReadOnlyList<StartupItem>>(items);
    }

    public Task<StartupBackupRecord> DisableAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var location = ParseLocation(item.Location);
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using var key = baseKey.OpenSubKey(location.Path, writable: true)
            ?? throw new InvalidOperationException("注册表启动项位置不存在。");
        var valueName = item.Metadata?["ValueName"] ?? item.Name;
        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
            ?? throw new InvalidOperationException("注册表启动项已被外部删除。");
        var kind = key.GetValueKind(valueName);

        var backup = new StartupBackupRecord
        {
            StartupItemId = item.Id,
            SourceType = SourceType,
            Name = valueName,
            OriginalLocation = item.Location,
            RequiresElevation = location.Hive == RegistryHive.LocalMachine,
            WasEnabled = true
        };

        // 先把原始数据完整编码进备份记录。Capture 失败会在这里抛出，此时原值仍在注册表中，
        // 不会出现“值已删除但备份不可用”的永久丢失。
        RegistryValueSerialization.Capture(backup, value, kind);

        key.DeleteValue(valueName, throwOnMissingValue: true);
        return Task.FromResult(backup);
    }

    public Task RestoreAsync(StartupBackupRecord backup, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = RegistryValueSerialization.Decode(backup);
        var kind = (RegistryValueKind)(backup.OriginalRegistryValueKind ?? (int)RegistryValueKind.String);
        var location = ParseLocation(backup.OriginalLocation);
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using var key = baseKey.CreateSubKey(location.Path, writable: true);
        if (key.GetValue(backup.Name) is not null)
        {
            throw new IOException($"注册表值“{backup.Name}”已经存在，未覆盖。");
        }

        try
        {
            key.SetValue(backup.Name, value, kind);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"备份“{backup.Name}”的数据与注册表类型 {kind} 不匹配，无法写回。", ex);
        }

        return Task.CompletedTask;
    }

    internal static (RegistryHive Hive, RegistryView View, string Path) ParseLocation(string location)
    {
        var parts = location.Split('|', 3);
        if (parts.Length != 3 ||
            !Enum.TryParse<RegistryHive>(parts[0], out var hive) ||
            !Enum.TryParse<RegistryView>(parts[1], out var view))
        {
            throw new InvalidDataException("注册表启动项位置格式无效。");
        }
        return (hive, view, parts[2]);
    }
}

internal static class WindowsCommandLine
{
    public static (string Command, string Arguments) Split(string commandLine)
    {
        var value = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        if (value.StartsWith('"'))
        {
            var closingQuote = value.IndexOf('"', 1);
            return closingQuote > 0
                ? (value[1..closingQuote], value[(closingQuote + 1)..].Trim())
                : (value.Trim('"'), string.Empty);
        }

        var executableEnd = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (executableEnd >= 0)
        {
            executableEnd += 4;
            return (value[..executableEnd], value[executableEnd..].Trim());
        }

        var separator = value.IndexOf(' ');
        return separator < 0 ? (value, string.Empty) : (value[..separator], value[(separator + 1)..].Trim());
    }
}
