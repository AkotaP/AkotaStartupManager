using AkotaStartupManager.Application.Services;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Core.Tests.Services;

public sealed class ConditionEvaluatorTests
{
    [Fact]
    public async Task AndGroup_StopsAtFirstFailure()
    {
        var first = new TestCondition();
        var second = new TestCondition();
        var checker = new QueueChecker(false, true);
        var evaluator = new ConditionEvaluator([checker]);
        var group = new ConditionGroup { Operator = ConditionGroupOperator.And, Children = [first, second] };

        var result = await evaluator.EvaluateAsync(group, default);

        Assert.False(result.IsSatisfied);
        Assert.Single(result.Children!);
        Assert.Equal(1, checker.CheckCount);
    }

    [Fact]
    public async Task OrGroup_StopsAtFirstSuccess()
    {
        var checker = new QueueChecker(true, false);
        var evaluator = new ConditionEvaluator([checker]);
        var group = new ConditionGroup
        {
            Operator = ConditionGroupOperator.Or,
            Children = [new TestCondition(), new TestCondition()]
        };

        var result = await evaluator.EvaluateAsync(group, default);

        Assert.True(result.IsSatisfied);
        Assert.Single(result.Children!);
        Assert.Equal(1, checker.CheckCount);
    }

    [Fact]
    public async Task EmptyGroup_IsInvalid()
    {
        var evaluator = new ConditionEvaluator([]);
        var result = await evaluator.EvaluateAsync(new ConditionGroup(), default);
        Assert.False(result.IsSatisfied);
        Assert.Contains("不能为空", result.Message);
    }

    private sealed class TestCondition : StartupCondition
    {
        public override string DisplayName => "测试";
    }

    private sealed class QueueChecker(params bool[] values) : IConditionChecker
    {
        private readonly Queue<bool> _values = new(values);
        public int CheckCount { get; private set; }
        public Type ConditionType => typeof(TestCondition);
        public Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken)
        {
            CheckCount++;
            var value = _values.Dequeue();
            return Task.FromResult(new ConditionResult(condition.Id, value, value ? "满足" : "不满足"));
        }
    }
}
