using AkotaStartupManager.Application.Services;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Core.Tests.Services;

public sealed class StartupOrchestratorTests
{
    [Fact]
    public async Task RequiresConsecutiveSuccesses_BeforeLaunching()
    {
        var evaluator = new SequenceEvaluator(false, true, false, true, true, true);
        var launcher = new FakeLauncher();
        var orchestrator = new StartupOrchestrator(evaluator, launcher, new NullLogger());
        var entry = CreateEntry();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrator.StateChanged += (_, state) =>
        {
            if (state.Status == ManagedEntryStatus.Running) completed.TrySetResult();
        };

        await orchestrator.StartAsync([entry], timeout.Token);
        await completed.Task.WaitAsync(timeout.Token);

        Assert.Equal(1, launcher.LaunchCount);
        Assert.Equal(6, evaluator.CheckCount);
    }

    [Fact]
    public async Task AlreadyRunning_DoesNotLaunchAgain()
    {
        var evaluator = new SequenceEvaluator(true);
        var launcher = new FakeLauncher { Running = true };
        var orchestrator = new StartupOrchestrator(evaluator, launcher, new NullLogger());
        var entry = CreateEntry();
        entry.RequiredConsecutiveSuccesses = 1;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        orchestrator.StateChanged += (_, state) =>
        {
            if (state.Status == ManagedEntryStatus.Running) completed.TrySetResult();
        };

        await orchestrator.StartAsync([entry], timeout.Token);
        await completed.Task.WaitAsync(timeout.Token);

        Assert.Equal(0, launcher.LaunchCount);
    }

    private static ManagedStartupEntry CreateEntry() => new()
    {
        Name = "测试程序",
        ExecutablePath = @"C:\test.exe",
        PollIntervalSeconds = 1,
        RequiredConsecutiveSuccesses = 3,
        StartupVerificationSeconds = 0,
        MaxRetries = 3,
        Conditions = new ConditionGroup { Children = [new ProcessCondition { ProcessName = "dependency" }] }
    };

    private sealed class SequenceEvaluator(params bool[] values) : IConditionEvaluator
    {
        private readonly Queue<bool> _values = new(values);
        public int CheckCount { get; private set; }
        public Task<ConditionResult> EvaluateAsync(StartupCondition condition, CancellationToken cancellationToken)
        {
            CheckCount++;
            var value = _values.Count > 0 ? _values.Dequeue() : true;
            return Task.FromResult(new ConditionResult(condition.Id, value, value ? "满足" : "不满足"));
        }
    }

    private sealed class FakeLauncher : IProcessLauncher
    {
        public bool Running { get; set; }
        public int LaunchCount { get; private set; }
        public bool IsRunning(string processName) => Running;
        public Task<LaunchResult> LaunchAsync(ManagedStartupEntry entry, CancellationToken cancellationToken)
        {
            LaunchCount++;
            Running = true;
            return Task.FromResult(new LaunchResult(LaunchResultKind.Started, 1));
        }
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Information(string message) { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
