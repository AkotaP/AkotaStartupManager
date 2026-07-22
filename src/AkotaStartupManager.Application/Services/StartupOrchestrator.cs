using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.Application.Services;

public sealed class StartupOrchestrator(
    IConditionEvaluator conditionEvaluator,
    IProcessLauncher processLauncher,
    IAppLogger logger)
{
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _monitoringCancellation;
    private readonly Dictionary<Guid, ManagedEntryRuntimeState> _states = [];

    public event EventHandler<ManagedEntryRuntimeState>? StateChanged;

    public IReadOnlyCollection<ManagedEntryRuntimeState> States
    {
        get
        {
            lock (_syncRoot)
            {
                return _states.Values.ToArray();
            }
        }
    }

    public async Task StartAsync(IEnumerable<ManagedStartupEntry> entries, CancellationToken cancellationToken = default)
    {
        await StopAsync();
        _monitoringCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _monitoringCancellation.Token;

        foreach (var entry in entries)
        {
            _ = MonitorEntrySafelyAsync(entry, token);
        }
    }

    public Task StopAsync()
    {
        _monitoringCancellation?.Cancel();
        _monitoringCancellation?.Dispose();
        _monitoringCancellation = null;
        return Task.CompletedTask;
    }

    public Task<ConditionResult> CheckNowAsync(ManagedStartupEntry entry, CancellationToken cancellationToken = default) =>
        conditionEvaluator.EvaluateAsync(entry.Conditions, cancellationToken);

    public async Task<LaunchResult> LaunchNowAsync(ManagedStartupEntry entry, CancellationToken cancellationToken = default)
    {
        SetState(entry, ManagedEntryStatus.Starting, "正在立即启动");
        var result = await processLauncher.LaunchAsync(entry, cancellationToken);
        SetState(entry,
            result.Kind == LaunchResultKind.Failed ? ManagedEntryStatus.Failed : ManagedEntryStatus.Running,
            result.Kind switch
            {
                LaunchResultKind.Started => "已启动",
                LaunchResultKind.AlreadyRunning => "程序已在运行",
                _ => $"启动失败：{result.Error}"
            });
        return result;
    }

    private async Task MonitorEntrySafelyAsync(ManagedStartupEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await MonitorEntryAsync(entry, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetState(entry, ManagedEntryStatus.Cancelled, "监控已停止");
        }
        catch (Exception ex)
        {
            logger.Error($"“{entry.Name}”监控发生未处理异常", ex);
            SetState(entry, ManagedEntryStatus.Failed, $"监控异常：{ex.Message}");
        }
    }

    private async Task MonitorEntryAsync(ManagedStartupEntry entry, CancellationToken cancellationToken)
    {
        if (!entry.IsEnabled)
        {
            SetState(entry, ManagedEntryStatus.Disabled, "规则已禁用");
            return;
        }

        ValidateEntry(entry);
        var consecutiveSuccesses = 0;
        SetState(entry, ManagedEntryStatus.Waiting, "等待条件满足");

        while (!cancellationToken.IsCancellationRequested)
        {
            SetState(entry, ManagedEntryStatus.Checking, "正在检查条件", consecutiveSuccesses);
            var result = await conditionEvaluator.EvaluateAsync(entry.Conditions, cancellationToken);

            if (result.IsSatisfied)
            {
                consecutiveSuccesses++;
                logger.Information($"{entry.Name}：条件连续满足 {consecutiveSuccesses}/{entry.RequiredConsecutiveSuccesses}");
            }
            else
            {
                if (consecutiveSuccesses > 0)
                {
                    logger.Information($"{entry.Name}：条件不再满足，连续计数清零；{result.Message}");
                }
                consecutiveSuccesses = 0;
            }

            SetState(entry, ManagedEntryStatus.Waiting, result.Message, consecutiveSuccesses);
            if (consecutiveSuccesses >= entry.RequiredConsecutiveSuccesses)
            {
                logger.Information($"{entry.Name}：依赖已就绪");
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(entry.PollIntervalSeconds), cancellationToken);
        }

        if (entry.DelayAfterReadySeconds > 0)
        {
            SetState(entry, ManagedEntryStatus.ReadyDelay, $"就绪后等待 {entry.DelayAfterReadySeconds} 秒", consecutiveSuccesses);
            await Task.Delay(TimeSpan.FromSeconds(entry.DelayAfterReadySeconds), cancellationToken);
        }

        await LaunchWithRetryAsync(entry, cancellationToken);
    }

    private async Task LaunchWithRetryAsync(ManagedStartupEntry entry, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= entry.MaxRetries; attempt++)
        {
            if (processLauncher.IsRunning(entry.ProcessName))
            {
                logger.Information($"{entry.Name}：目标已运行，跳过重复启动");
                SetState(entry, ManagedEntryStatus.Running, "目标已在运行", attempt: attempt);
                return;
            }

            SetState(entry, ManagedEntryStatus.Starting, $"正在启动（第 {attempt}/{entry.MaxRetries} 次）", attempt: attempt);
            logger.Information($"{entry.Name}：开始第 {attempt} 次启动");
            var launchResult = await processLauncher.LaunchAsync(entry, cancellationToken);

            if (launchResult.Kind == LaunchResultKind.AlreadyRunning)
            {
                SetState(entry, ManagedEntryStatus.Running, "目标已在运行", attempt: attempt);
                return;
            }

            if (launchResult.Kind == LaunchResultKind.Failed)
            {
                logger.Warning($"{entry.Name}：启动失败：{launchResult.Error}");
                continue;
            }

            SetState(entry, ManagedEntryStatus.Verifying, $"等待 {entry.StartupVerificationSeconds} 秒验证进程", attempt: attempt);
            await Task.Delay(TimeSpan.FromSeconds(entry.StartupVerificationSeconds), cancellationToken);
            if (processLauncher.IsRunning(entry.ProcessName))
            {
                logger.Information($"{entry.Name}：启动成功");
                SetState(entry, ManagedEntryStatus.Running, "启动成功", attempt: attempt);
                return;
            }

            logger.Warning($"{entry.Name}：启动后进程未保持运行");
        }

        SetState(entry, ManagedEntryStatus.Failed, $"启动失败，已重试 {entry.MaxRetries} 次", attempt: entry.MaxRetries);
    }

    private static void ValidateEntry(ManagedStartupEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ExecutablePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(entry.PollIntervalSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(entry.RequiredConsecutiveSuccesses, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(entry.MaxRetries, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(entry.DelayAfterReadySeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(entry.StartupVerificationSeconds);
    }

    private void SetState(
        ManagedStartupEntry entry,
        ManagedEntryStatus status,
        string message,
        int consecutiveSuccesses = 0,
        int attempt = 0)
    {
        var state = new ManagedEntryRuntimeState(
            entry.Id, status, message, consecutiveSuccesses, attempt, DateTimeOffset.Now);
        lock (_syncRoot)
        {
            _states[entry.Id] = state;
        }
        StateChanged?.Invoke(this, state);
    }
}
