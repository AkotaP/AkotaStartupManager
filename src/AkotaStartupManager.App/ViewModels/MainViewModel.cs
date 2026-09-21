using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WpfApplication = System.Windows.Application;
using AkotaStartupManager.Application.Services;
using AkotaStartupManager.Core.Interfaces;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;
using AkotaStartupManager.Infrastructure.Logging;
using AkotaStartupManager.Infrastructure.Windows;

namespace AkotaStartupManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly StartupInventoryService _inventory;
    private readonly StartupManagementService _management;
    private readonly IConfigurationRepository _repository;
    private readonly StartupOrchestrator _orchestrator;
    private readonly SelfStartupService _selfStartup;
    private readonly PrivilegeService _privilege;
    private readonly IPortablePathService _paths;
    private readonly SemaphoreSlim _configurationGate = new(1, 1);
    private int _selectedPage;
    private bool _isBusy;
    private StartupItem? _selectedStartupItem;
    private ManagedStartupEntry? _selectedManagedEntry;
    private string _statusText = "正在初始化…";
    private string _searchText = string.Empty;

    public MainViewModel(
        StartupInventoryService inventory,
        StartupManagementService management,
        IConfigurationRepository repository,
        StartupOrchestrator orchestrator,
        SelfStartupService selfStartup,
        PrivilegeService privilege,
        IPortablePathService paths,
        FileAppLogger logger)
    {
        _inventory = inventory;
        _management = management;
        _repository = repository;
        _orchestrator = orchestrator;
        _selfStartup = selfStartup;
        _privilege = privilege;
        _paths = paths;
        RefreshCommand = new AsyncCommand(RefreshAsync, () => !IsBusy);
        DisableCommand = new AsyncCommand(DisableSelectedAsync, () => SelectedStartupItem is not null && !IsBusy);
        TakeOverCommand = new AsyncCommand(TakeOverSelectedAsync, () => SelectedStartupItem is not null && !IsBusy);
        RestoreCommand = new AsyncCommand(RestoreLastAsync, () => Backups.Count > 0 && !IsBusy);
        AddManagedCommand = new AsyncCommand(AddManagedAsync, () => !IsBusy);
        EditManagedCommand = new AsyncCommand(EditManagedAsync, () => SelectedManagedEntry is not null && !IsBusy);
        RemoveManagedCommand = new AsyncCommand(RemoveManagedAsync, () => SelectedManagedEntry is not null && !IsBusy);
        LaunchNowCommand = new AsyncCommand(LaunchSelectedNowAsync, () => SelectedManagedEntry is not null && !IsBusy);
        RestartMonitoringCommand = new AsyncCommand(RestartMonitoringAsync, () => !IsBusy);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        Backups.CollectionChanged += (_, _) => RestoreCommand.NotifyCanExecuteChanged();
        logger.MessageWritten += (_, line) => WpfApplication.Current.Dispatcher.Invoke(() => LogLines.Insert(0, line));
        _orchestrator.StateChanged += (_, state) => WpfApplication.Current.Dispatcher.Invoke(() => UpdateRuntimeState(state));
    }

    public ObservableCollection<StartupItem> StartupItems { get; } = [];
    public ObservableCollection<ManagedStartupEntry> ManagedEntries { get; } = [];
    public ObservableCollection<StartupBackupRecord> Backups { get; } = [];
    public ObservableCollection<ManagedEntryRuntimeState> RuntimeStates { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand DisableCommand { get; }
    public AsyncCommand TakeOverCommand { get; }
    public AsyncCommand RestoreCommand { get; }
    public AsyncCommand AddManagedCommand { get; }
    public AsyncCommand EditManagedCommand { get; }
    public AsyncCommand RemoveManagedCommand { get; }
    public AsyncCommand LaunchNowCommand { get; }
    public AsyncCommand RestartMonitoringCommand { get; }
    public RelayCommand OpenLogsCommand { get; }

    public int SelectedPage { get => _selectedPage; set => SetProperty(ref _selectedPage, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) NotifyBusyCommandsCanExecuteChanged();
        }
    }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) ApplyFilter(); } }
    public StartupItem? SelectedStartupItem
    {
        get => _selectedStartupItem;
        set
        {
            if (!SetProperty(ref _selectedStartupItem, value)) return;
            DisableCommand.NotifyCanExecuteChanged();
            TakeOverCommand.NotifyCanExecuteChanged();
        }
    }
    public ManagedStartupEntry? SelectedManagedEntry
    {
        get => _selectedManagedEntry;
        set
        {
            if (!SetProperty(ref _selectedManagedEntry, value)) return;
            EditManagedCommand.NotifyCanExecuteChanged();
            RemoveManagedCommand.NotifyCanExecuteChanged();
            LaunchNowCommand.NotifyCanExecuteChanged();
        }
    }
    public bool StartWithWindows { get => _selfStartup.IsEnabled; set { _selfStartup.SetEnabled(value); OnPropertyChanged(); } }
    public int StartupItemCount => StartupItems.Count;
    public int ManagedCount => ManagedEntries.Count;
    public int RunningCount => RuntimeStates.Count(x => x.Status == ManagedEntryStatus.Running);

    private IReadOnlyList<StartupItem> _allStartupItems = [];

    public async Task InitializeAsync()
    {
        var configuration = await _repository.LoadAsync();
        ManagedEntries.Clear();
        Backups.Clear();
        foreach (var entry in configuration.Entries) ManagedEntries.Add(entry);
        foreach (var backup in configuration.Backups) Backups.Add(backup);
        await RefreshAsync();
        await _orchestrator.StartAsync(ManagedEntries);
        StatusText = "监控运行中";
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "正在扫描 Windows 启动项…";
        try
        {
            _allStartupItems = await _inventory.GetAllAsync();
            ApplyFilter();
            StatusText = $"已发现 {_allStartupItems.Count} 个启动项";
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败";
            System.Windows.MessageBox.Show(ex.Message, "扫描启动项失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allStartupItems
            : _allStartupItems.Where(x => x.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                                          x.Command.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
        StartupItems.Clear();
        foreach (var item in filtered) StartupItems.Add(item);
        OnPropertyChanged(nameof(StartupItemCount));
    }

    private bool EnsureElevationIfRequired(StartupItem item, string action)
    {
        if (!item.RequiresElevation || _privilege.IsAdministrator) return true;
        if (System.Windows.MessageBox.Show(
                $"“{item.Name}”属于系统范围，{action}需要管理员权限。是否以管理员身份重新启动？",
                "需要管理员权限", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
        {
            _privilege.RestartElevated("--show-page", "1");
            ((global::AkotaStartupManager.App.App)WpfApplication.Current).ExitApplication();
        }
        return false;
    }

    private async Task DisableSelectedAsync()
    {
        if (SelectedStartupItem is null) return;
        if (!EnsureElevationIfRequired(SelectedStartupItem, "禁用此启动项")) return;
        if (System.Windows.MessageBox.Show($"将禁用“{SelectedStartupItem.Name}”并保存可恢复备份。继续吗？", "确认禁用",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await ExecuteBusyAsync(async () =>
        {
            await _management.DisableAsync(SelectedStartupItem);
            await ReloadConfigurationAsync();
            await RefreshAsync();
        }, "禁用启动项失败");
    }

    private async Task TakeOverSelectedAsync()
    {
        if (SelectedStartupItem is null) return;
        var item = SelectedStartupItem;
        if (!EnsureElevationIfRequired(item, "接管此启动项")) return;
        var executablePath = item.Command;
        if (!File.Exists(executablePath) || !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "可执行程序 (*.exe)|*.exe",
                Title = $"为“{item.Name}”选择实际可执行文件"
            };
            if (dialog.ShowDialog() != true) return;
            executablePath = dialog.FileName;
        }

        var entry = new ManagedStartupEntry
        {
            Name = item.Name,
            ExecutablePath = executablePath,
            Arguments = item.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(item.WorkingDirectory)
                ? Path.GetDirectoryName(executablePath) ?? string.Empty
                : item.WorkingDirectory,
            Conditions = new ConditionGroup
            {
                Operator = ConditionGroupOperator.And,
                Children = [new ProcessCondition { ProcessName = "explorer" }]
            }
        };
        var editor = new global::AkotaStartupManager.App.ManagedEntryEditorWindow(entry)
        {
            Owner = WpfApplication.Current.MainWindow
        };
        if (editor.ShowDialog() != true) return;
        if (System.Windows.MessageBox.Show(
                $"将保存“{entry.Name}”的接管规则，并禁用原生启动项。继续吗？",
                "确认接管", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async () =>
        {
            await _management.TakeOverAsync(item, entry);
            await ReloadConfigurationAsync();
            await RefreshAsync();
            await _orchestrator.StartAsync(ManagedEntries);
        }, "接管启动项失败");
    }

    private async Task RestoreLastAsync()
    {
        var backup = Backups.LastOrDefault();
        if (backup is null) return;
        await ExecuteBusyAsync(async () =>
        {
            await _management.RestoreAsync(backup);
            await ReloadConfigurationAsync();
            await RefreshAsync();
        }, "恢复启动项失败");
    }

    private async Task AddManagedAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "可执行程序 (*.exe)|*.exe", Title = "选择要接管启动的程序" };
        if (dialog.ShowDialog() != true) return;
        var entry = new ManagedStartupEntry
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            ExecutablePath = dialog.FileName,
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty,
            Conditions = new ConditionGroup
            {
                Operator = ConditionGroupOperator.And,
                Children = [new ProcessCondition { ProcessName = "explorer" }]
            }
        };
        var editor = new global::AkotaStartupManager.App.ManagedEntryEditorWindow(entry) { Owner = WpfApplication.Current.MainWindow };
        if (editor.ShowDialog() != true) return;
        await ExecuteBusyAsync(async () =>
        {
            var configuration = await _repository.LoadAsync();
            configuration.Entries.Add(entry);
            await _repository.SaveAsync(configuration);
            await ReloadConfigurationAsync();
            await _orchestrator.StartAsync(ManagedEntries);
        }, "保存接管规则失败");
    }

    private async Task EditManagedAsync()
    {
        if (SelectedManagedEntry is null) return;
        var selectedId = SelectedManagedEntry.Id;
        var editedEntry = SelectedManagedEntry.DeepClone();
        var editor = new global::AkotaStartupManager.App.ManagedEntryEditorWindow(editedEntry, isEditing: true)
        {
            Owner = WpfApplication.Current.MainWindow
        };
        if (editor.ShowDialog() != true) return;

        await ExecuteBusyAsync(async () =>
        {
            var configuration = await _repository.LoadAsync();
            var index = configuration.Entries.FindIndex(x => x.Id == selectedId);
            if (index < 0)
                throw new InvalidOperationException("要编辑的接管规则已不存在，请刷新后重试。");
            configuration.Entries[index] = editedEntry;
            await _repository.SaveAsync(configuration);
            await ReloadConfigurationAsync();
            SelectedManagedEntry = ManagedEntries.FirstOrDefault(x => x.Id == selectedId);
            await _orchestrator.StartAsync(ManagedEntries);
            StatusText = $"已保存规则“{editedEntry.Name}”";
        }, "保存接管规则失败");
    }

    private async Task RemoveManagedAsync()
    {
        if (SelectedManagedEntry is null) return;
        var selected = SelectedManagedEntry;
        if (System.Windows.MessageBox.Show($"确定删除接管规则“{selected.Name}”吗？", "删除规则", MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await ExecuteBusyAsync(async () =>
        {
            var configuration = await _repository.LoadAsync();
            configuration.Entries.RemoveAll(x => x.Id == selected.Id);
            await _repository.SaveAsync(configuration);
            await ReloadConfigurationAsync();
            await _orchestrator.StartAsync(ManagedEntries);
        }, "删除规则失败");
    }

    private async Task LaunchSelectedNowAsync()
    {
        if (SelectedManagedEntry is not null) await _orchestrator.LaunchNowAsync(SelectedManagedEntry);
    }

    /// <summary>
    /// 切换单条接管规则的启用状态：先写入配置，再按新状态重启监控。
    /// 返回 false 时模型值已回滚，调用方需要让界面绑定重新取值。
    /// </summary>
    public async Task<bool> SetManagedEntryEnabledAsync(ManagedStartupEntry entry, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(entry);
        // 配置读取与写入是“读—改—写”组合，连续点击时需要串行执行，避免后一次覆盖前一次。
        await _configurationGate.WaitAsync();
        try
        {
            var succeeded = await ExecuteBusyAsync(async () =>
            {
                var configuration = await _repository.LoadAsync();
                var target = configuration.Entries.FirstOrDefault(x => x.Id == entry.Id)
                    ?? throw new InvalidOperationException("要更新的接管规则已不存在，请刷新后重试。");
                target.IsEnabled = isEnabled;
                await _repository.SaveAsync(configuration);
                entry.IsEnabled = isEnabled;
                await _orchestrator.StartAsync(ManagedEntries);
                StatusText = isEnabled ? $"已启用规则“{entry.Name}”" : $"已停用规则“{entry.Name}”";
            }, "更新启用状态失败");

            if (!succeeded) entry.IsEnabled = !isEnabled;
            return succeeded;
        }
        finally { _configurationGate.Release(); }
    }

    public async Task RestartMonitoringAsync()
    {
        await _orchestrator.StartAsync(ManagedEntries);
        StatusText = "监控已重新开始";
    }

    private async Task ReloadConfigurationAsync()
    {
        var configuration = await _repository.LoadAsync();
        ManagedEntries.Clear();
        Backups.Clear();
        foreach (var entry in configuration.Entries) ManagedEntries.Add(entry);
        foreach (var backup in configuration.Backups) Backups.Add(backup);
        OnPropertyChanged(nameof(ManagedCount));
    }

    private void NotifyBusyCommandsCanExecuteChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        DisableCommand.NotifyCanExecuteChanged();
        TakeOverCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
        AddManagedCommand.NotifyCanExecuteChanged();
        EditManagedCommand.NotifyCanExecuteChanged();
        RemoveManagedCommand.NotifyCanExecuteChanged();
        LaunchNowCommand.NotifyCanExecuteChanged();
        RestartMonitoringCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> ExecuteBusyAsync(Func<Task> action, string errorTitle)
    {
        IsBusy = true;
        try { await action(); return true; }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally { IsBusy = false; }
    }

    private void UpdateRuntimeState(ManagedEntryRuntimeState state)
    {
        var existing = RuntimeStates.FirstOrDefault(x => x.EntryId == state.EntryId);
        if (existing is not null) RuntimeStates.Remove(existing);
        RuntimeStates.Add(state);
        StatusText = state.Message;
        OnPropertyChanged(nameof(RunningCount));
    }

    private void OpenLogs() => Process.Start(new ProcessStartInfo { FileName = _paths.LogsDirectory, UseShellExecute = true });
}
