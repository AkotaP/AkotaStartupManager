using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Windows;
using AkotaStartupManager.App.Services;
using AkotaStartupManager.App.ViewModels;
using AkotaStartupManager.Application.Services;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Infrastructure.Logging;
using AkotaStartupManager.Infrastructure.Persistence;
using AkotaStartupManager.Infrastructure.Windows;
using AkotaStartupManager.Infrastructure.Windows.ConditionCheckers;

namespace AkotaStartupManager.App;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\AkotaStartupManager.SingleInstance";
    private const string PipeName = "AkotaStartupManager.Activate";
    private Mutex? _mutex;
    private TrayIconService? _tray;
    private StartupOrchestrator? _orchestrator;
    private FileAppLogger? _logger;
    private bool _exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            await NotifyExistingInstanceAsync();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Error("界面发生未处理异常", args.Exception);
            System.Windows.MessageBox.Show(args.Exception.Message, "Akota Startup Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.Error("后台任务发生未观察异常", args.Exception);
            args.SetObserved();
        };

        try
        {
            var paths = new PortablePathService();
            paths.EnsureWritable();
            var repository = new JsonConfigurationRepository(paths);
            _logger = new FileAppLogger(paths);
            var providers = new IStartupProvider[]
            {
                new RegistryStartupProvider(),
                new StartupFolderProvider(paths),
                new ScheduledTaskStartupProvider()
            };
            var checkers = new IConditionChecker[]
            {
                new ProcessConditionChecker(),
                new TcpPortConditionChecker(),
                new HttpConditionChecker(new HttpClient()),
                new FileExistsConditionChecker(),
                new WindowsServiceConditionChecker(),
                new WindowTitleConditionChecker()
            };
            var evaluator = new ConditionEvaluator(checkers);
            _orchestrator = new StartupOrchestrator(evaluator, new ProcessLauncher(), _logger);
            var viewModel = new MainViewModel(
                new StartupInventoryService(providers),
                new StartupManagementService(providers, repository),
                repository, _orchestrator, new SelfStartupService(), new PrivilegeService(), paths, _logger);
            var window = new MainWindow(viewModel);
            MainWindow = window;
            _tray = new TrayIconService(window, viewModel);
            _ = ListenForActivationAsync(window);
            await viewModel.InitializeAsync();
            var pageArgumentIndex = Array.FindIndex(e.Args, x => x.Equals("--show-page", StringComparison.OrdinalIgnoreCase));
            if (pageArgumentIndex >= 0 && pageArgumentIndex + 1 < e.Args.Length && int.TryParse(e.Args[pageArgumentIndex + 1], out var page))
            {
                viewModel.SelectedPage = Math.Clamp(page, 0, 4);
            }
            if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase))
            {
                window.Show();
            }
            _logger.Information("Akota Startup Manager 已启动");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        }
    }

    public async void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        if (_orchestrator is not null) await _orchestrator.StopAsync();
        _logger?.Information("Akota Startup Manager 正在退出");
        _tray?.Dispose();
        if (MainWindow is MainWindow window) window.ExitApplication();
        Shutdown();
    }

    private async Task ListenForActivationAsync(MainWindow window)
    {
        while (!_exiting)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync();
                await Dispatcher.InvokeAsync(() =>
                {
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                });
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                if (!_exiting) _logger?.Warning($"单实例唤醒监听异常：{ex.Message}");
            }
        }
    }

    private static async Task NotifyExistingInstanceAsync()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500);
        }
        catch (TimeoutException)
        {
            // 已有实例可能仍在启动，避免创建第二个监控器。
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _logger?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
