using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AkotaStartupManager.App.ViewModels;

namespace AkotaStartupManager.App;

public partial class BackupListWindow : Window
{
    private readonly MainViewModel _viewModel;

    public BackupListWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // 恢复系统级备份需要提权时程序会整体重启，先关闭本窗口再做重启。
        _viewModel.ElevationRestartRequested += OnElevationRestartRequested;
        Closed += (_, _) => _viewModel.ElevationRestartRequested -= OnElevationRestartRequested;
    }

    private void OnElevationRestartRequested(object? sender, EventArgs e) => Close();

    private void BackupGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin) return;
        var source = origin;
        while (source is not null and not DataGridRow)
        {
            source = VisualTreeHelper.GetParent(source);
        }
        if (source is not DataGridRow) return;

        e.Handled = true;
        if (_viewModel.RestoreBackupCommand.CanExecute(null)) _viewModel.RestoreBackupCommand.Execute(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
