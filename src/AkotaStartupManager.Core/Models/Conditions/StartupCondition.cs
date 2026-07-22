using System.ComponentModel;
using System.Runtime.CompilerServices;
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
public abstract class StartupCondition : INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsEnabled { get; set; } = true;
    public abstract string DisplayName { get; }
    public abstract StartupCondition DeepClone();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected bool SetDisplayProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName)) return false;
        OnPropertyChanged(nameof(DisplayName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum ConditionGroupOperator
{
    And,
    Or
}

public sealed class ConditionGroup : StartupCondition
{
    private ConditionGroupOperator _operator = ConditionGroupOperator.And;

    public ConditionGroupOperator Operator
    {
        get => _operator;
        set => SetDisplayProperty(ref _operator, value);
    }

    public List<StartupCondition> Children { get; set; } = [];
    public override string DisplayName => Operator == ConditionGroupOperator.And ? "全部满足" : "任一满足";
    public override StartupCondition DeepClone() => new ConditionGroup
    {
        Id = Id,
        IsEnabled = IsEnabled,
        Operator = Operator,
        Children = Children.Select(x => x.DeepClone()).ToList()
    };
}

public sealed class ProcessCondition : StartupCondition
{
    private string _processName = string.Empty;

    public string ProcessName
    {
        get => _processName;
        set => SetDisplayProperty(ref _processName, value);
    }

    public override string DisplayName => $"进程：{ProcessName}";
    public override StartupCondition DeepClone() => new ProcessCondition
    {
        Id = Id,
        IsEnabled = IsEnabled,
        ProcessName = ProcessName
    };
}

public sealed class TcpPortCondition : StartupCondition
{
    private string _host = "127.0.0.1";
    private int _port;

    public string Host
    {
        get => _host;
        set => SetDisplayProperty(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => SetDisplayProperty(ref _port, value);
    }

    public int TimeoutMilliseconds { get; set; } = 1500;
    public override string DisplayName => $"TCP：{Host}:{Port}";
    public override StartupCondition DeepClone() => new TcpPortCondition
    {
        Id = Id,
        IsEnabled = IsEnabled,
        Host = Host,
        Port = Port,
        TimeoutMilliseconds = TimeoutMilliseconds
    };
}

public sealed class HttpCondition : StartupCondition
{
    private string _url = "http://127.0.0.1/";

    public string Url
    {
        get => _url;
        set => SetDisplayProperty(ref _url, value);
    }

    public int TimeoutMilliseconds { get; set; } = 3000;
    public override string DisplayName => $"HTTP：{Url}";
    public override StartupCondition DeepClone() => new HttpCondition
    {
        Id = Id,
        IsEnabled = IsEnabled,
        Url = Url,
        TimeoutMilliseconds = TimeoutMilliseconds
    };
}

public sealed class FileExistsCondition : StartupCondition
{
    private string _path = string.Empty;

    public string Path
    {
        get => _path;
        set => SetDisplayProperty(ref _path, value);
    }

    public override string DisplayName => $"文件：{Path}";
    public override StartupCondition DeepClone() => new FileExistsCondition
    {
        Id = Id,
        IsEnabled = IsEnabled,
        Path = Path
    };
}

public sealed class WindowsServiceCondition : StartupCondition
{
    private string _serviceName = string.Empty;

    public string ServiceName
    {
        get => _serviceName;
        set => SetDisplayProperty(ref _serviceName, value);
    }

    public override string DisplayName => $"服务：{ServiceName}";
    public override StartupCondition DeepClone() => new WindowsServiceCondition
    {
        Id = Id,
        IsEnabled = IsEnabled,
        ServiceName = ServiceName
    };
}

public sealed class WindowTitleCondition : StartupCondition
{
    private string _titlePattern = string.Empty;

    public string TitlePattern
    {
        get => _titlePattern;
        set => SetDisplayProperty(ref _titlePattern, value);
    }

    public bool UseRegularExpression { get; set; }
    public override string DisplayName => $"窗口：{TitlePattern}";
    public override StartupCondition DeepClone() => new WindowTitleCondition
    {
        Id = Id,
        IsEnabled = IsEnabled,
        TitlePattern = TitlePattern,
        UseRegularExpression = UseRegularExpression
    };
}
