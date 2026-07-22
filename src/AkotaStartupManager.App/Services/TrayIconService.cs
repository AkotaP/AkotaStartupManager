using System.Drawing;
using Forms = System.Windows.Forms;
using AkotaStartupManager.App.ViewModels;

namespace AkotaStartupManager.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _trayIcon;
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;

    public TrayIconService(MainWindow window, MainViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;
        using var iconStream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Resources/app.ico"))?.Stream
            ?? throw new InvalidOperationException("无法加载应用图标资源。");
        _trayIcon = new Icon(iconStream);
        var menu = new Forms.ContextMenuStrip();
        var status = new Forms.ToolStripMenuItem("当前状态：初始化中") { Enabled = false };
        menu.Items.Add(status);
        menu.Items.Add("显示主窗口", null, (_, _) => ShowWindow());
        menu.Items.Add("立即检查", null, async (_, _) => await _viewModel.RefreshAsync());
        menu.Items.Add("立即启动选中项", null, (_, _) => _viewModel.LaunchNowCommand.Execute(null));
        menu.Items.Add("重新开始监控", null, async (_, _) => await _viewModel.RestartMonitoringAsync());
        menu.Items.Add("打开日志", null, (_, _) => _viewModel.OpenLogsCommand.Execute(null));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => App.Current.Dispatcher.Invoke(((App)App.Current).ExitApplication));
        _icon = new Forms.NotifyIcon
        {
            Text = "Akota Startup Manager",
            Icon = _trayIcon,
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => ShowWindow();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.StatusText))
            {
                status.Text = $"当前状态：{_viewModel.StatusText}";
            }
        };
    }

    private void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == System.Windows.WindowState.Minimized)
            _window.WindowState = System.Windows.WindowState.Normal;
        _window.Activate();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _trayIcon.Dispose();
    }
}
