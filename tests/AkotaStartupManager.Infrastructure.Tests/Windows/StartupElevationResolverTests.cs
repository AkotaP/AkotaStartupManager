using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Infrastructure.Windows;

namespace AkotaStartupManager.Infrastructure.Tests.Windows;

public sealed class StartupElevationResolverTests
{
    [Fact]
    public void Registry_CurrentUser_DoesNotRequireElevation()
    {
        var backup = Registry(@"CurrentUser|Registry64|Software\Microsoft\Windows\CurrentVersion\Run");

        Assert.False(StartupElevationResolver.RequiresElevation(backup));
    }

    [Theory]
    [InlineData("LocalMachine|Registry64|Software\\Microsoft\\Windows\\CurrentVersion\\Run")]
    [InlineData("LocalMachine|Registry32|Software\\Microsoft\\Windows\\CurrentVersion\\Run")]
    [InlineData("LocalMachine|Registry64|Software\\Microsoft\\Windows\\CurrentVersion\\RunOnce")]
    public void Registry_LocalMachine_RequiresElevation(string location)
    {
        Assert.True(StartupElevationResolver.RequiresElevation(Registry(location)));
    }

    [Fact]
    public void StartupFolder_CommonFolder_RequiresElevation()
    {
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        var backup = new StartupBackupRecord
        {
            SourceType = StartupSourceType.StartupFolder,
            OriginalLocation = Path.Combine(common, "app.lnk")
        };

        Assert.True(StartupElevationResolver.RequiresElevation(backup));
    }

    [Fact]
    public void StartupFolder_UserFolder_DoesNotRequireElevation()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var backup = new StartupBackupRecord
        {
            SourceType = StartupSourceType.StartupFolder,
            OriginalLocation = Path.Combine(user, "app.lnk")
        };

        Assert.False(StartupElevationResolver.RequiresElevation(backup));
    }

    [Fact]
    public void ScheduledTask_SystemPath_RequiresElevation()
    {
        var backup = new StartupBackupRecord
        {
            SourceType = StartupSourceType.ScheduledTask,
            Metadata = new Dictionary<string, string> { ["TaskPath"] = @"\Microsoft\Windows\UpdateOrchestrator\", ["TaskName"] = "Task" }
        };

        Assert.True(StartupElevationResolver.RequiresElevation(backup));
    }

    [Fact]
    public void ScheduledTask_CustomPath_DoesNotRequireElevation()
    {
        var backup = new StartupBackupRecord
        {
            SourceType = StartupSourceType.ScheduledTask,
            Metadata = new Dictionary<string, string> { ["TaskPath"] = @"\MyTasks\", ["TaskName"] = "Task" }
        };

        Assert.False(StartupElevationResolver.RequiresElevation(backup));
    }

    [Fact]
    public void RecordedFlag_TakesPrecedenceOverLocation()
    {
        var backup = Registry(@"LocalMachine|Registry64|Software\Microsoft\Windows\CurrentVersion\Run");
        backup.RequiresElevation = false;

        Assert.False(StartupElevationResolver.RequiresElevation(backup));
    }

    [Fact]
    public void MalformedRegistryLocation_DoesNotThrow()
    {
        Assert.False(StartupElevationResolver.RequiresElevation(Registry("not-a-location")));
    }

    private static StartupBackupRecord Registry(string location) => new()
    {
        SourceType = StartupSourceType.Registry,
        OriginalLocation = location,
        Name = "app"
    };
}
