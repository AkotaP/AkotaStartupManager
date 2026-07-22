using System.Net.Sockets;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.Infrastructure.Windows.ConditionCheckers;

public sealed class ProcessConditionChecker : IConditionChecker
{
    public Type ConditionType => typeof(ProcessCondition);

    public Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken)
    {
        var process = (ProcessCondition)condition;
        var name = Path.GetFileNameWithoutExtension(process.ProcessName);
        var found = !string.IsNullOrWhiteSpace(name) && System.Diagnostics.Process.GetProcessesByName(name).Length > 0;
        return Task.FromResult(new ConditionResult(condition.Id, found,
            found ? $"检测到进程 {name}" : $"进程 {name} 尚未运行"));
    }
}

public sealed class TcpPortConditionChecker : IConditionChecker
{
    public Type ConditionType => typeof(TcpPortCondition);

    public async Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken)
    {
        var tcp = (TcpPortCondition)condition;
        if (tcp.Port is < 1 or > 65535)
        {
            return new ConditionResult(condition.Id, false, "TCP 端口必须在 1 到 65535 之间");
        }

        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(tcp.TimeoutMilliseconds, 100, 30_000)));
        try
        {
            await client.ConnectAsync(tcp.Host, tcp.Port, timeout.Token);
            return new ConditionResult(condition.Id, true, $"端口 {tcp.Host}:{tcp.Port} 已监听");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ConditionResult(condition.Id, false, $"端口 {tcp.Host}:{tcp.Port} 尚未就绪");
        }
    }
}

public sealed class HttpConditionChecker(HttpClient httpClient) : IConditionChecker
{
    public Type ConditionType => typeof(HttpCondition);

    public async Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken)
    {
        var http = (HttpCondition)condition;
        if (!Uri.TryCreate(http.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !IsLoopback(uri.Host))
        {
            return new ConditionResult(condition.Id, false, "仅支持本机 HTTP/HTTPS 地址");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(http.TimeoutMilliseconds, 100, 30_000)));
        try
        {
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return new ConditionResult(condition.Id, response.IsSuccessStatusCode,
                response.IsSuccessStatusCode
                    ? $"接口返回成功：{(int)response.StatusCode}"
                    : $"接口返回：{(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ConditionResult(condition.Id, false, $"接口尚未就绪：{ex.Message}");
        }
    }

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address);
}

public sealed class FileExistsConditionChecker : IConditionChecker
{
    public Type ConditionType => typeof(FileExistsCondition);

    public Task<ConditionResult> CheckAsync(StartupCondition condition, CancellationToken cancellationToken)
    {
        var file = (FileExistsCondition)condition;
        var exists = !string.IsNullOrWhiteSpace(file.Path) && File.Exists(Environment.ExpandEnvironmentVariables(file.Path));
        return Task.FromResult(new ConditionResult(condition.Id, exists,
            exists ? $"文件已存在：{file.Path}" : $"文件尚不存在：{file.Path}"));
    }
}
