using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.Application.Services;

public sealed class StartupInventoryService(IEnumerable<IStartupProvider> providers)
{
    public async Task<IReadOnlyList<StartupItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = providers.Select(provider => GetSafelyAsync(provider, cancellationToken));
        var groups = await Task.WhenAll(tasks);
        return groups.SelectMany(x => x)
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<StartupItem>> GetSafelyAsync(
        IStartupProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.GetItemsAsync(cancellationToken);
        }
        catch
        {
            return [];
        }
    }
}

public sealed class StartupManagementService(
    IEnumerable<IStartupProvider> providers,
    IConfigurationRepository repository)
{
    private readonly Dictionary<StartupSourceType, IStartupProvider> _providers =
        providers.ToDictionary(x => x.SourceType);

    public async Task DisableAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        var configuration = await repository.LoadAsync(cancellationToken);
        var backup = await GetProvider(item.SourceType).DisableAsync(item, cancellationToken);
        configuration.Backups.RemoveAll(x => x.StartupItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
        configuration.Backups.Add(backup);

        try
        {
            await repository.SaveAsync(configuration, cancellationToken);
        }
        catch
        {
            await GetProvider(item.SourceType).RestoreAsync(backup, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 恢复一条备份。已写入的原生启动项不会因为之后的配置写入失败而回滚，
    /// 但这种情况会以明确的异常上报，不会被当成成功。
    /// </summary>
    public async Task RestoreAsync(
        StartupBackupRecord backup,
        bool removeLinkedManagedRule = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backup);
        await GetProvider(backup.SourceType).RestoreAsync(backup, cancellationToken);

        var configuration = await repository.LoadAsync(cancellationToken);
        configuration.Backups.RemoveAll(x => x.StartupItemId.Equals(backup.StartupItemId, StringComparison.OrdinalIgnoreCase));
        if (removeLinkedManagedRule)
        {
            configuration.Entries.RemoveAll(x => x.OriginalStartupItemId is { } id &&
                                                 id.Equals(backup.StartupItemId, StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            await repository.SaveAsync(configuration, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"原生启动项“{backup.Name}”已恢复，但备份记录未更新成功：{ex.Message}", ex);
        }
    }

    public async Task TakeOverAsync(
        StartupItem item,
        ManagedStartupEntry entry,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(entry.ExecutablePath))
        {
            throw new FileNotFoundException("待启动程序不存在。", entry.ExecutablePath);
        }

        var configuration = await repository.LoadAsync(cancellationToken);
        entry.OriginalStartupItemId = item.Id;
        configuration.Entries.Add(entry);
        await repository.SaveAsync(configuration, cancellationToken);

        try
        {
            var backup = await GetProvider(item.SourceType).DisableAsync(item, cancellationToken);
            configuration.Backups.Add(backup);
            await repository.SaveAsync(configuration, cancellationToken);
        }
        catch
        {
            configuration.Entries.Remove(entry);
            await repository.SaveAsync(configuration, cancellationToken);
            throw;
        }
    }

    private IStartupProvider GetProvider(StartupSourceType sourceType) =>
        _providers.TryGetValue(sourceType, out var provider)
            ? provider
            : throw new NotSupportedException($"不支持的启动项来源：{sourceType}");
}
