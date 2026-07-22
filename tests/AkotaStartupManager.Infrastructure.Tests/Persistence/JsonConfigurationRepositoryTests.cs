using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;
using AkotaStartupManager.Infrastructure.Persistence;

namespace AkotaStartupManager.Infrastructure.Tests.Persistence;

public sealed class JsonConfigurationRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AkotaTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_PreservesPolymorphicConditions()
    {
        var paths = new TestPaths(_directory);
        var repository = new JsonConfigurationRepository(paths);
        var configuration = new ApplicationConfiguration
        {
            Entries =
            [
                new ManagedStartupEntry
                {
                    Name = "A",
                    ExecutablePath = @"C:\A.exe",
                    Conditions = new ConditionGroup
                    {
                        Operator = ConditionGroupOperator.Or,
                        Children = [new TcpPortCondition { Port = 8080 }, new FileExistsCondition { Path = @"C:\ready" }]
                    }
                }
            ]
        };

        await repository.SaveAsync(configuration);
        var loaded = await repository.LoadAsync();

        Assert.Single(loaded.Entries);
        Assert.Equal(ConditionGroupOperator.Or, loaded.Entries[0].Conditions.Operator);
        Assert.IsType<TcpPortCondition>(loaded.Entries[0].Conditions.Children[0]);
        Assert.IsType<FileExistsCondition>(loaded.Entries[0].Conditions.Children[1]);
    }

    [Fact]
    public async Task CorruptCurrentConfiguration_LoadsPreviousBackup()
    {
        var paths = new TestPaths(_directory);
        var repository = new JsonConfigurationRepository(paths);
        await repository.SaveAsync(new ApplicationConfiguration { StartWithWindows = true });
        await repository.SaveAsync(new ApplicationConfiguration { StartWithWindows = false });
        await File.WriteAllTextAsync(Path.Combine(paths.DataDirectory, "config.json"), "not-json");

        var loaded = await repository.LoadAsync();

        Assert.True(loaded.StartWithWindows);
        Assert.NotEmpty(Directory.GetFiles(paths.DataDirectory, "config.damaged-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class TestPaths(string root) : AkotaStartupManager.Core.Interfaces.IPortablePathService
    {
        public string BaseDirectory => root;
        public string DataDirectory => Path.Combine(root, "Data");
        public string BackupDirectory => Path.Combine(DataDirectory, "Backups");
        public string LogsDirectory => Path.Combine(root, "Logs");
        public void EnsureWritable()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }
    }
}
