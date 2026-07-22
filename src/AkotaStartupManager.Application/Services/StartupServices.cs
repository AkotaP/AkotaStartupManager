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

    public async Task RestoreAsync(StartupBackupRecord backup, CancellationToken cancellationToken = default)
    {
        await GetProvider(backup.SourceType).RestoreAsync(backup, cancellationToken);
        var configuration = await repository.LoadAsync(cancellationToken);
        configuration.Backups.RemoveAll(x => x.StartupItemId.Equals(backup.StartupItemId, StringComparison.OrdinalIgnoreCase));
        await repository.SaveAsync(configuration, cancellationToken);
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
