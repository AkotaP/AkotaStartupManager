using AkotaStartupManager.Core.Interfaces;

namespace AkotaStartupManager.Infrastructure.Logging;

public sealed class FileAppLogger(IPortablePathService paths) : IAppLogger, IDisposable
{
    private readonly object _syncRoot = new();
    private StreamWriter? _writer;
    private DateOnly _writerDate;

    public event EventHandler<string>? MessageWritten;

    public void Information(string message) => Write("INF", message);
    public void Warning(string message) => Write("WRN", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        lock (_syncRoot)
        {
            paths.EnsureWritable();
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (_writer is null || _writerDate != today)
            {
                _writer?.Dispose();
                var path = Path.Combine(paths.LogsDirectory, $"akota-{today:yyyyMMdd}.log");
                _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    AutoFlush = true
                };
                _writerDate = today;
                DeleteExpiredLogs();
            }

            var line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
            _writer.WriteLine(line);
            MessageWritten?.Invoke(this, line);
        }
    }

    private void DeleteExpiredLogs()
    {
        var cutoff = DateTime.Now.AddDays(-30);
        foreach (var file in Directory.EnumerateFiles(paths.LogsDirectory, "akota-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
                // 日志清理失败不应影响主流程。
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
