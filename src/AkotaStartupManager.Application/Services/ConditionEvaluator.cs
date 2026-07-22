using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Application.Services;

public sealed class ConditionEvaluator(IEnumerable<IConditionChecker> checkers) : IConditionEvaluator
{
    private readonly Dictionary<Type, IConditionChecker> _checkers = checkers.ToDictionary(x => x.ConditionType);

    public async Task<ConditionResult> EvaluateAsync(
        StartupCondition condition,
        CancellationToken cancellationToken)
    {
        if (!condition.IsEnabled)
        {
            return new ConditionResult(condition.Id, true, "条件已禁用，视为满足");
        }

        if (condition is ConditionGroup group)
        {
            return await EvaluateGroupAsync(group, cancellationToken);
        }

        if (!_checkers.TryGetValue(condition.GetType(), out var checker))
        {
            return new ConditionResult(condition.Id, false, $"没有 {condition.GetType().Name} 的检查器");
        }

        try
        {
            return await checker.CheckAsync(condition, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ConditionResult(condition.Id, false, $"检查失败：{ex.Message}");
        }
    }

    private async Task<ConditionResult> EvaluateGroupAsync(
        ConditionGroup group,
        CancellationToken cancellationToken)
    {
        var enabledChildren = group.Children.Where(x => x.IsEnabled).ToList();
        if (enabledChildren.Count == 0)
        {
            return new ConditionResult(group.Id, false, "条件组不能为空");
        }

        var results = new List<ConditionResult>(enabledChildren.Count);
        var satisfied = group.Operator == ConditionGroupOperator.And;

        foreach (var child in enabledChildren)
        {
            var result = await EvaluateAsync(child, cancellationToken);
            results.Add(result);

            if (group.Operator == ConditionGroupOperator.And && !result.IsSatisfied)
            {
                satisfied = false;
                break;
            }

            if (group.Operator == ConditionGroupOperator.Or && result.IsSatisfied)
            {
                satisfied = true;
                break;
            }
        }

        var message = satisfied ? $"{group.DisplayName}：已满足" : $"{group.DisplayName}：未满足";
        return new ConditionResult(group.Id, satisfied, message, results);
    }
}
