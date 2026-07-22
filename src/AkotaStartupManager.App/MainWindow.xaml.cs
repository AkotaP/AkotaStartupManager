using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }
}
