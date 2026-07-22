using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Infrastructure.Windows.ConditionCheckers;

public sealed class WindowsServiceConditionChecker : IConditionChecker
{
    public Type ConditionType => typeof(WindowsServiceCondition);

    public async Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken)
    {
        var serviceCondition = (WindowsServiceCondition)condition;
        if (string.IsNullOrWhiteSpace(serviceCondition.ServiceName))
        {
            return new ConditionResult(condition.Id, false, "服务名不能为空");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"query \"{serviceCondition.ServiceName.Replace("\"", string.Empty)}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ConditionResult(condition.Id, false, "无法启动服务状态查询");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var running = process.ExitCode == 0 && output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            return new ConditionResult(condition.Id, running,
                running ? $"服务 {serviceCondition.ServiceName} 正在运行" : $"服务 {serviceCondition.ServiceName} 尚未运行");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ConditionResult(condition.Id, false, $"无法读取服务：{ex.Message}");
        }
    }
}

public sealed class WindowTitleConditionChecker : IConditionChecker
{
    public Type ConditionType => typeof(WindowTitleCondition);

    public Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken)
    {
        var window = (WindowTitleCondition)condition;
        if (string.IsNullOrWhiteSpace(window.TitlePattern))
        {
            return Task.FromResult(new ConditionResult(condition.Id, false, "窗口标题条件不能为空"));
        }

        Regex? regex = null;
        if (window.UseRegularExpression)
        {
            try
            {
                regex = new Regex(window.TitlePattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException ex)
            {
                return Task.FromResult(new ConditionResult(condition.Id, false, $"正则表达式无效：{ex.Message}"));
            }
        }

        string? matchedTitle = null;
        EnumWindows((handle, _) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsWindowVisible(handle) || GetWindowTextLength(handle) == 0)
            {
                return true;
            }

            var builder = new StringBuilder(GetWindowTextLength(handle) + 1);
            _ = GetWindowText(handle, builder, builder.Capacity);
            var title = builder.ToString();
            var matched = regex?.IsMatch(title) ?? title.Contains(window.TitlePattern, StringComparison.CurrentCultureIgnoreCase);
            if (!matched)
            {
                return true;
            }

            matchedTitle = title;
            return false;
        }, IntPtr.Zero);

        return Task.FromResult(new ConditionResult(condition.Id, matchedTitle is not null,
            matchedTitle is null ? $"未找到窗口：{window.TitlePattern}" : $"检测到窗口：{matchedTitle}"));
    }

    private delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maximumCount);
}

public sealed class ProcessLauncher : IProcessLauncher
{
    public bool IsRunning(string processName) =>
        !string.IsNullOrWhiteSpace(processName) && Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)).Length > 0;

    public Task<LaunchResult> LaunchAsync(ManagedStartupEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsRunning(entry.ProcessName))
        {
            return Task.FromResult(new LaunchResult(LaunchResultKind.AlreadyRunning));
        }

        if (!File.Exists(entry.ExecutablePath))
        {
            return Task.FromResult(new LaunchResult(LaunchResultKind.Failed, Error: "可执行文件不存在"));
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = entry.ExecutablePath,
                Arguments = entry.Arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(entry.WorkingDirectory)
                    ? Path.GetDirectoryName(entry.ExecutablePath) ?? AppContext.BaseDirectory
                    : entry.WorkingDirectory,
                UseShellExecute = true
            };
            var process = Process.Start(startInfo);
            return Task.FromResult(process is null
                ? new LaunchResult(LaunchResultKind.Failed, Error: "系统未返回进程信息")
                : new LaunchResult(LaunchResultKind.Started, process.Id));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Task.FromResult(new LaunchResult(LaunchResultKind.Failed, Error: ex.Message));
        }
    }
}
