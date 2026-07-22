using System.Text.Json.Serialization;

namespace AkotaStartupManager.Core.Models.Conditions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ConditionGroup), "group")]
[JsonDerivedType(typeof(ProcessCondition), "process")]
[JsonDerivedType(typeof(TcpPortCondition), "tcpPort")]
[JsonDerivedType(typeof(HttpCondition), "http")]
[JsonDerivedType(typeof(FileExistsCondition), "fileExists")]
[JsonDerivedType(typeof(WindowsServiceCondition), "windowsService")]
[JsonDerivedType(typeof(WindowTitleCondition), "windowTitle")]
public abstract class StartupCondition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsEnabled { get; set; } = true;
    public abstract string DisplayName { get; }
}

public enum ConditionGroupOperator
{
    And,
    Or
}

public sealed class ConditionGroup : StartupCondition
{
    public ConditionGroupOperator Operator { get; set; } = ConditionGroupOperator.And;
    public List<StartupCondition> Children { get; set; } = [];
    public override string DisplayName => Operator == ConditionGroupOperator.And ? "全部满足" : "任一满足";
}

public sealed class ProcessCondition : StartupCondition
{
    public string ProcessName { get; set; } = string.Empty;
    public override string DisplayName => $"进程：{ProcessName}";
}

public sealed class TcpPortCondition : StartupCondition
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; }
    public int TimeoutMilliseconds { get; set; } = 1500;
    public override string DisplayName => $"TCP：{Host}:{Port}";
}

public sealed class HttpCondition : StartupCondition
{
    public string Url { get; set; } = "http://127.0.0.1/";
    public int TimeoutMilliseconds { get; set; } = 3000;
    public override string DisplayName => $"HTTP：{Url}";
}

public sealed class FileExistsCondition : StartupCondition
{
    public string Path { get; set; } = string.Empty;
    public override string DisplayName => $"文件：{Path}";
}

public sealed class WindowsServiceCondition : StartupCondition
{
    public string ServiceName { get; set; } = string.Empty;
    public override string DisplayName => $"服务：{ServiceName}";
}

public sealed class WindowTitleCondition : StartupCondition
{
    public string TitlePattern { get; set; } = string.Empty;
    public bool UseRegularExpression { get; set; }
    public override string DisplayName => $"窗口：{TitlePattern}";
}
