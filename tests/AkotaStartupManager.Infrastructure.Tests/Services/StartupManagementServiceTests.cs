using AkotaStartupManager.Application.Services;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Infrastructure.Persistence;

namespace AkotaStartupManager.Infrastructure.Tests.Services;

public sealed class StartupManagementServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AkotaTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RestoreAsync_KeepsLinkedManagedRuleByDefault()
    {
        var (service, repository, provider) = await CreateServiceAsync();

        await service.RestoreAsync((await repository.LoadAsync()).Backups[0]);

        var loaded = await repository.LoadAsync();
        Assert.True(provider.IsRestored);
        Assert.Single(loaded.Entries);
        Assert.Empty(loaded.Backups);
    }

    /// <summary>
    /// 恢复一条已被接管的启动项时，接管规则一并删除，否则程序在登录时会被启动两次。
    /// </summary>
    [Fact]
    public async Task RestoreAsync_RemovesLinkedManagedRuleOnRequest()
    {
        var (service, repository, provider) = await CreateServiceAsync();

        await service.RestoreAsync((await repository.LoadAsync()).Backups[0], removeLinkedManagedRule: true);

        var loaded = await repository.LoadAsync();
        Assert.True(provider.IsRestored);
        Assert.Empty(loaded.Entries);
        Assert.Empty(loaded.Backups);
    }

    [Fact]
    public async Task RestoreAsync_RemovesOnlyTheRuleLinkedToThisBackup()
    {
        var (service, repository, _) = await CreateServiceAsync();
        var configuration = await repository.LoadAsync();
        configuration.Entries.Add(new ManagedStartupEntry { Name = "无关规则", OriginalStartupItemId = "registry:other" });
        await repository.SaveAsync(configuration);

        await service.RestoreAsync((await repository.LoadAsync()).Backups[0], removeLinkedManagedRule: true);

        var loaded = await repository.LoadAsync();
        Assert.Single(loaded.Entries);
        Assert.Equal("无关规则", loaded.Entries[0].Name);
    }

    [Fact]
    public async Task RestoreAsync_ReportsWhenConfigurationCannotBeSaved()
    {
        var (service, repository, _) = await CreateServiceAsync();
        var backup = (await repository.LoadAsync()).Backups[0];
        var failing = new StartupManagementService(
            [new FakeProvider()],
            new SaveFailingRepository(repository));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => failing.RestoreAsync(backup, removeLinkedManagedRule: true));

        Assert.Contains("已恢复", exception.Message);
        Assert.Contains("未更新成功", exception.Message);
        Assert.Contains(backup.Name, exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private async Task<(StartupManagementService Service, JsonConfigurationRepository Repository, FakeProvider Provider)>
        CreateServiceAsync()
    {
        var paths = new TestPaths(_root);
        var repository = new JsonConfigurationRepository(paths);
        var provider = new FakeProvider();
        var service = new StartupManagementService([provider], repository);

        var item = new StartupItem("registry:probe", "Probe App", @"C:\App\app.exe", string.Empty,
            @"C:\App", StartupSourceType.Registry, StartupScope.CurrentUser, true, false, "probe-location");
        var backup = await provider.DisableAsync(item);
        await repository.SaveAsync(new ApplicationConfiguration
        {
            Entries = [new ManagedStartupEntry { Name = "接管规则", OriginalStartupItemId = item.Id }],
            Backups = [backup]
        });

        return (service, repository, provider);
    }

    private sealed class FakeProvider : IStartupProvider
    {
        public StartupSourceType SourceType => StartupSourceType.Registry;
        public bool IsRestored { get; private set; }

        public Task<IReadOnlyList<StartupItem>> GetItemsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StartupItem>>([]);

        public Task<StartupBackupRecord> DisableAsync(StartupItem item, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StartupBackupRecord
            {
                StartupItemId = item.Id,
                SourceType = SourceType,
                Name = item.Name,
                OriginalLocation = item.Location,
                OriginalValue = item.Command,
                OriginalRegistryValueKind = 1,
                WasEnabled = true
            });

        public Task RestoreAsync(StartupBackupRecord backup, CancellationToken cancellationToken = default)
        {
            IsRestored = true;
            return Task.CompletedTask;
        }
    }

    private sealed class SaveFailingRepository(IConfigurationRepository inner) : IConfigurationRepository
    {
        public Task<ApplicationConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
            inner.LoadAsync(cancellationToken);

        public Task SaveAsync(ApplicationConfiguration configuration, CancellationToken cancellationToken = default) =>
            throw new IOException("磁盘不可写。");
    }
}
