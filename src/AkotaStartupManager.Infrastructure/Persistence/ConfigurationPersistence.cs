using System.Text.Json;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.Infrastructure.Persistence;

public sealed class PortablePathService : IPortablePathService
{
    public string BaseDirectory { get; } = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    public string DataDirectory => Path.Combine(BaseDirectory, "Data");
    public string BackupDirectory => Path.Combine(DataDirectory, "Backups");
    public string LogsDirectory => Path.Combine(BaseDirectory, "Logs");

    public void EnsureWritable()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogsDirectory);
        var probe = Path.Combine(DataDirectory, $".write-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "ok");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException($"程序目录不可写：{BaseDirectory}。请将便携版移到有写入权限的位置。", ex);
        }
        finally
        {
            if (File.Exists(probe))
            {
                File.Delete(probe);
            }
        }
    }
}

public sealed class JsonConfigurationRepository(IPortablePathService paths) : IConfigurationRepository
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private string ConfigurationPath => Path.Combine(paths.DataDirectory, "config.json");
    private string BackupPath => Path.Combine(paths.DataDirectory, "config.previous.json");

    public async Task<ApplicationConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureWritable();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(ConfigurationPath))
            {
                return new ApplicationConfiguration();
            }

            try
            {
                return await DeserializeAsync(ConfigurationPath, cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                var damagedPath = Path.Combine(paths.DataDirectory, $"config.damaged-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Copy(ConfigurationPath, damagedPath, overwrite: false);
                if (File.Exists(BackupPath))
                {
                    return await DeserializeAsync(BackupPath, cancellationToken);
                }

                throw new InvalidDataException($"配置文件已损坏，原文件已保留为 {damagedPath}", ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ApplicationConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        paths.EnsureWritable();
        await _gate.WaitAsync(cancellationToken);
        var temporaryPath = ConfigurationPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(stream, configuration, _options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(ConfigurationPath))
            {
                File.Replace(temporaryPath, ConfigurationPath, BackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, ConfigurationPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            _gate.Release();
        }
    }

    private async Task<ApplicationConfiguration> DeserializeAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var configuration = await JsonSerializer.DeserializeAsync<ApplicationConfiguration>(stream, _options, cancellationToken);
        return configuration ?? throw new JsonException("配置文件为空。");
    }
}
