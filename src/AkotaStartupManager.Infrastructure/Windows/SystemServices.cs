using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace AkotaStartupManager.Infrastructure.Windows;

public sealed class SelfStartupService
{
    private const string ValueName = "AkotaStartupManager";
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName)?.ToString() is { } value &&
                   value.Contains(Environment.ProcessPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定当前程序路径。");
        key.SetValue(ValueName, $"\"{executable}\" --background", RegistryValueKind.String);
    }
}

public sealed class PrivilegeService
{
    public bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool RestartElevated(params string[] arguments)
    {
        if (IsAdministrator)
        {
            return false;
        }

        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定当前程序路径。");
        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = string.Join(' ', arguments.Select(QuoteArgument)),
            UseShellExecute = true,
            Verb = "runas"
        });
        return true;
    }

    private static string QuoteArgument(string argument) =>
        argument.Any(char.IsWhiteSpace) ? $"\"{argument.Replace("\"", "\\\"")}\"" : argument;
}
