using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Core.Tests.Models;

public sealed class ManagedStartupEntryTests
{
    [Fact]
    public void DeepClone_PreservesFieldsAndSeparatesNestedConditions()
    {
        var entryId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var nestedId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var tcpId = Guid.NewGuid();
        var httpId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var windowId = Guid.NewGuid();
        var original = new ManagedStartupEntry
        {
            Id = entryId,
            Name = "Original",
            ExecutablePath = @"C:\Apps\Example.exe",
            Arguments = "--example",
            WorkingDirectory = @"C:\Apps",
            IsEnabled = false,
            PollIntervalSeconds = 7,
            RequiredConsecutiveSuccesses = 4,
            DelayAfterReadySeconds = 3,
            StartupVerificationSeconds = 12,
            MaxRetries = 5,
            OriginalStartupItemId = "registry:example",
            Conditions = new ConditionGroup
            {
                Id = rootId,
                IsEnabled = false,
                Operator = ConditionGroupOperator.Or,
                Children =
                [
                    new ProcessCondition { Id = processId, IsEnabled = false, ProcessName = "explorer" },
                    new ConditionGroup
                    {
                        Id = nestedId,
                        Operator = ConditionGroupOperator.And,
                        Children =
                        [
                            new TcpPortCondition { Id = tcpId, Host = "localhost", Port = 5432, TimeoutMilliseconds = 900 },
                            new HttpCondition { Id = httpId, Url = "http://127.0.0.1:8080/health", TimeoutMilliseconds = 1200 },
                            new FileExistsCondition { Id = fileId, Path = @"C:\ready.flag" },
                            new WindowsServiceCondition { Id = serviceId, ServiceName = "ExampleService" },
                            new WindowTitleCondition { Id = windowId, TitlePattern = "Example.*", UseRegularExpression = true }
                        ]
                    }
                ]
            }
        };

        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
        Assert.Equal(entryId, clone.Id);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.ExecutablePath, clone.ExecutablePath);
        Assert.Equal(original.Arguments, clone.Arguments);
        Assert.Equal(original.WorkingDirectory, clone.WorkingDirectory);
        Assert.Equal(original.IsEnabled, clone.IsEnabled);
        Assert.Equal(original.PollIntervalSeconds, clone.PollIntervalSeconds);
        Assert.Equal(original.RequiredConsecutiveSuccesses, clone.RequiredConsecutiveSuccesses);
        Assert.Equal(original.DelayAfterReadySeconds, clone.DelayAfterReadySeconds);
        Assert.Equal(original.StartupVerificationSeconds, clone.StartupVerificationSeconds);
        Assert.Equal(original.MaxRetries, clone.MaxRetries);
        Assert.Equal(original.OriginalStartupItemId, clone.OriginalStartupItemId);

        Assert.NotSame(original.Conditions, clone.Conditions);
        Assert.Equal(rootId, clone.Conditions.Id);
        Assert.False(clone.Conditions.IsEnabled);
        Assert.Equal(ConditionGroupOperator.Or, clone.Conditions.Operator);
        var process = Assert.IsType<ProcessCondition>(clone.Conditions.Children[0]);
        Assert.Equal(processId, process.Id);
        Assert.False(process.IsEnabled);
        Assert.Equal("explorer", process.ProcessName);

        var nested = Assert.IsType<ConditionGroup>(clone.Conditions.Children[1]);
        Assert.NotSame(original.Conditions.Children[1], nested);
        Assert.Equal(nestedId, nested.Id);
        var tcp = Assert.IsType<TcpPortCondition>(nested.Children[0]);
        Assert.Equal((tcpId, "localhost", 5432, 900), (tcp.Id, tcp.Host, tcp.Port, tcp.TimeoutMilliseconds));
        var http = Assert.IsType<HttpCondition>(nested.Children[1]);
        Assert.Equal((httpId, "http://127.0.0.1:8080/health", 1200), (http.Id, http.Url, http.TimeoutMilliseconds));
        var file = Assert.IsType<FileExistsCondition>(nested.Children[2]);
        Assert.Equal((fileId, @"C:\ready.flag"), (file.Id, file.Path));
        var service = Assert.IsType<WindowsServiceCondition>(nested.Children[3]);
        Assert.Equal((serviceId, "ExampleService"), (service.Id, service.ServiceName));
        var window = Assert.IsType<WindowTitleCondition>(nested.Children[4]);
        Assert.Equal((windowId, "Example.*", true), (window.Id, window.TitlePattern, window.UseRegularExpression));

        clone.Name = "Changed";
        process.ProcessName = "changed";
        nested.Children.RemoveAt(0);

        Assert.Equal("Original", original.Name);
        Assert.Equal("explorer", ((ProcessCondition)original.Conditions.Children[0]).ProcessName);
        Assert.Equal(5, ((ConditionGroup)original.Conditions.Children[1]).Children.Count);
    }
}
