using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AkotaStartupManager.App.ViewModels;

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

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }
}
