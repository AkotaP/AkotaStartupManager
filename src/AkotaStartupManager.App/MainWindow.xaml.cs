using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AkotaStartupManager.App.ViewModels;
using AkotaStartupManager.Core.Models;

namespace AkotaStartupManager.App;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    private void Navigation_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is System.Windows.Controls.RadioButton { Tag: string tag } && int.TryParse(tag, out var page))
        {
            viewModel.SelectedPage = page;
        }
    }

    private void ManagedEntriesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 双击“启用”列的复选框只是快速切换两次启用状态，不应弹出编辑窗口。
        if (e.OriginalSource is DependencyObject origin && FindAncestor<System.Windows.Controls.CheckBox>(origin) is not null) return;
        var source = e.OriginalSource as DependencyObject;
        while (source is not null and not DataGridRow)
        {
            source = VisualTreeHelper.GetParent(source);
        }
        if (source is not DataGridRow || DataContext is not MainViewModel viewModel ||
            !viewModel.EditManagedCommand.CanExecute(null)) return;
        viewModel.EditManagedCommand.Execute(null);
        e.Handled = true;
    }

    private async void ManagedEntryEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox || checkBox.DataContext is not ManagedStartupEntry entry ||
            DataContext is not MainViewModel viewModel) return;
        if (await viewModel.SetManagedEntryEnabledAsync(entry, checkBox.IsChecked == true)) return;

        // 保存失败时已回滚模型值，这里让绑定重新从模型取值，避免界面停留在未生效的状态。
        checkBox.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T target) return target;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }
}
