using System.Diagnostics;
using System.Text.Json;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.Infrastructure.Windows;

public sealed class ScheduledTaskStartupProvider : IStartupProvider
{
    public StartupSourceType SourceType => StartupSourceType.ScheduledTask;

    public async Task<IReadOnlyList<StartupItem>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        var json = await RunPowerShellAsync(
            "$ErrorActionPreference='Stop'; Get-ScheduledTask | Where-Object { $_.Triggers.CimClass.CimClassName -match 'MSFT_Task_(Boot|Logon)Trigger' } | ForEach-Object { [pscustomobject]@{ TaskPath=$_.TaskPath; TaskName=$_.TaskName; State=$_.State.ToString(); Enabled=$_.Settings.Enabled; Execute=($_.Actions | Select-Object -First 1).Execute; Arguments=($_.Actions | Select-Object -First 1).Arguments; WorkingDirectory=($_.Actions | Select-Object -First 1).WorkingDirectory } } | ConvertTo-Json -Compress",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        var elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToArray()
            : [document.RootElement];
        var items = new List<StartupItem>();
        foreach (var element in elements)
        {
            var taskPath = GetString(element, "TaskPath");
            var taskName = GetString(element, "TaskName");
            var command = GetString(element, "Execute");
            items.Add(new StartupItem(
                $"scheduledTask:{taskPath}{taskName}", taskName, command,
                GetString(element, "Arguments"), GetString(element, "WorkingDirectory"), SourceType,
                IsSystemTask(taskPath) ? StartupScope.AllUsers : StartupScope.CurrentUser,
                element.TryGetProperty("Enabled", out var enabled) && enabled.GetBoolean(),
                true, $"{taskPath}{taskName}",
                new Dictionary<string, string> { ["TaskPath"] = taskPath, ["TaskName"] = taskName }));
        }
        return items;
    }

    public async Task<StartupBackupRecord> DisableAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        var taskPath = item.Metadata?["TaskPath"] ?? "\\";
        var taskName = item.Metadata?["TaskName"] ?? item.Name;
        await RunPowerShellAsync(
            $"$ErrorActionPreference='Stop'; Disable-ScheduledTask -TaskPath {Quote(taskPath)} -TaskName {Quote(taskName)} | Out-Null",
            cancellationToken);
        return new StartupBackupRecord
        {
            StartupItemId = item.Id,
            SourceType = SourceType,
            Name = taskName,
            OriginalLocation = item.Location,
            RequiresElevation = item.RequiresElevation,
            WasEnabled = item.IsEnabled,
            Metadata = new Dictionary<string, string> { ["TaskPath"] = taskPath, ["TaskName"] = taskName }
        };
    }

    public async Task RestoreAsync(StartupBackupRecord backup, CancellationToken cancellationToken = default)
    {
        if (!backup.WasEnabled)
        {
            return;
        }
        var taskPath = backup.Metadata.GetValueOrDefault("TaskPath", "\\");
        var taskName = backup.Metadata.GetValueOrDefault("TaskName", backup.Name);
        await RunPowerShellAsync(
            $"$ErrorActionPreference='Stop'; Enable-ScheduledTask -TaskPath {Quote(taskPath)} -TaskName {Quote(taskName)} | Out-Null",
            cancellationToken);
    }

    private static async Task<string> RunPowerShellAsync(string script, CancellationToken cancellationToken)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 PowerShell。");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "计划任务操作失败。" : error.Trim());
        }
        return output.Trim();
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    private static bool IsSystemTask(string path) => path.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase);
    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : string.Empty;
}
