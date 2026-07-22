using System.IO;
using System.Windows;
using AkotaStartupManager.Core.Models;
using AkotaStartupManager.Core.Models.Conditions;

namespace AkotaStartupManager.App;

public partial class ManagedEntryEditorWindow : Window
{
    private readonly ManagedStartupEntry _entry;
    private StartupCondition? _selectedCondition;
    private bool _updatingFields;

    public ManagedEntryEditorWindow(ManagedStartupEntry entry, bool isEditing = false)
    {
        InitializeComponent();
        _entry = entry;
        DataContext = entry;
        ConditionTree.ItemsSource = new[] { entry.Conditions };
        if (isEditing)
        {
            Title = "编辑已有接管规则";
            EditorHeading.Text = "编辑接管启动规则";
            EditorDescription.Text = "修改将仅在保存成功后生效；取消不会改变现有规则。";
        }
    }

    private ConditionGroup SelectedGroup =>
        _selectedCondition as ConditionGroup ?? FindParent(_entry.Conditions, _selectedCondition) ?? _entry.Conditions;

    private void AddAndGroup_Click(object sender, RoutedEventArgs e) => Add(new ConditionGroup { Operator = ConditionGroupOperator.And });
    private void AddOrGroup_Click(object sender, RoutedEventArgs e) => Add(new ConditionGroup { Operator = ConditionGroupOperator.Or });
    private void AddProcess_Click(object sender, RoutedEventArgs e) => Add(new ProcessCondition { ProcessName = "explorer" });
    private void AddTcp_Click(object sender, RoutedEventArgs e) => Add(new TcpPortCondition { Port = 8080 });
    private void AddHttp_Click(object sender, RoutedEventArgs e) => Add(new HttpCondition { Url = "http://127.0.0.1:8080/" });
    private void AddFile_Click(object sender, RoutedEventArgs e) => Add(new FileExistsCondition { Path = @"C:\path\ready.flag" });
    private void AddService_Click(object sender, RoutedEventArgs e) => Add(new WindowsServiceCondition { ServiceName = "ServiceName" });
    private void AddWindow_Click(object sender, RoutedEventArgs e) => Add(new WindowTitleCondition { TitlePattern = "窗口标题" });

    private void Add(StartupCondition condition)
    {
        SelectedGroup.Children.Add(condition);
        RefreshTree();
    }

    private void RemoveCondition_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCondition is null || ReferenceEquals(_selectedCondition, _entry.Conditions))
        {
            System.Windows.MessageBox.Show("根条件组不能删除。", "条件表达式", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var parent = FindParent(_entry.Conditions, _selectedCondition);
        parent?.Children.Remove(_selectedCondition);
        _selectedCondition = null;
        PropertyPanel.Visibility = Visibility.Collapsed;
        RefreshTree();
    }

    private static ConditionGroup? FindParent(ConditionGroup group, StartupCondition? target)
    {
        if (target is null) return null;
        foreach (var child in group.Children)
        {
            if (ReferenceEquals(child, target)) return group;
            if (child is ConditionGroup nested && FindParent(nested, target) is { } parent) return parent;
        }
        return null;
    }

    private void RefreshTree()
    {
        ConditionTree.Items.Refresh();
        foreach (var item in ConditionTree.Items)
            if (ConditionTree.ItemContainerGenerator.ContainerFromItem(item) is System.Windows.Controls.TreeViewItem node)
                node.IsExpanded = true;
    }

    private void ConditionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selectedCondition = e.NewValue as StartupCondition;
        ShowProperties(_selectedCondition);
    }

    private void ShowProperties(StartupCondition? condition)
    {
        _updatingFields = true;
        try
        {
            PropertyPanel.Visibility = condition is null ? Visibility.Collapsed : Visibility.Visible;
            Field2Row.Visibility = Visibility.Collapsed;
            RegexCheck.Visibility = Visibility.Collapsed;
            Field1Text.Text = string.Empty;
            Field2Text.Text = string.Empty;
            switch (condition)
            {
                case ConditionGroup group:
                    PropertyTitle.Text = "条件组参数"; Field1Label.Text = "运算符 (And/Or)"; Field1Text.Text = group.Operator.ToString(); break;
                case ProcessCondition process:
                    PropertyTitle.Text = "进程条件"; Field1Label.Text = "进程名"; Field1Text.Text = process.ProcessName; break;
                case TcpPortCondition tcp:
                    PropertyTitle.Text = "TCP 条件"; Field1Label.Text = "主机"; Field1Text.Text = tcp.Host; Field2Label.Text = "端口"; Field2Text.Text = tcp.Port.ToString(); Field2Row.Visibility = Visibility.Visible; break;
                case HttpCondition http:
                    PropertyTitle.Text = "HTTP 条件"; Field1Label.Text = "本地 URL"; Field1Text.Text = http.Url; break;
                case FileExistsCondition file:
                    PropertyTitle.Text = "文件条件"; Field1Label.Text = "文件路径"; Field1Text.Text = file.Path; break;
                case WindowsServiceCondition service:
                    PropertyTitle.Text = "服务条件"; Field1Label.Text = "服务名"; Field1Text.Text = service.ServiceName; break;
                case WindowTitleCondition window:
                    PropertyTitle.Text = "窗口标题条件"; Field1Label.Text = "标题模式"; Field1Text.Text = window.TitlePattern; RegexCheck.IsChecked = window.UseRegularExpression; RegexCheck.Visibility = Visibility.Visible; break;
            }
        }
        finally { _updatingFields = false; }
    }

    private void ConditionField_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingFields || _selectedCondition is null) return;
        switch (_selectedCondition)
        {
            case ConditionGroup group when Enum.TryParse<ConditionGroupOperator>(Field1Text.Text, true, out var op): group.Operator = op; break;
            case ProcessCondition process: process.ProcessName = Field1Text.Text; break;
            case TcpPortCondition tcp:
                tcp.Host = Field1Text.Text;
                if (int.TryParse(Field2Text.Text, out var port)) tcp.Port = port;
                break;
            case HttpCondition http: http.Url = Field1Text.Text; break;
            case FileExistsCondition file: file.Path = Field1Text.Text; break;
            case WindowsServiceCondition service: service.ServiceName = Field1Text.Text; break;
            case WindowTitleCondition window:
                window.TitlePattern = Field1Text.Text;
                window.UseRegularExpression = RegexCheck.IsChecked == true;
                break;
        }
        RefreshTree();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_entry.Name) || string.IsNullOrWhiteSpace(_entry.ExecutablePath))
        {
            System.Windows.MessageBox.Show("名称和程序路径不能为空。", "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (!File.Exists(_entry.ExecutablePath))
        {
            System.Windows.MessageBox.Show("选择的程序不存在。", "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (!ValidateGroup(_entry.Conditions, out var error))
        {
            System.Windows.MessageBox.Show(error, "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (_entry.PollIntervalSeconds < 1 || _entry.RequiredConsecutiveSuccesses < 1 || _entry.MaxRetries < 1 || _entry.DelayAfterReadySeconds < 0 || _entry.StartupVerificationSeconds < 0)
        {
            System.Windows.MessageBox.Show("轮询、稳定性和重试参数超出合法范围。", "配置无效", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        DialogResult = true;
    }

    private static bool ValidateGroup(ConditionGroup group, out string error)
    {
        if (group.Children.Count == 0) { error = "条件组不能为空。"; return false; }
        foreach (var condition in group.Children)
        {
            if (condition is ConditionGroup nested && !ValidateGroup(nested, out error)) return false;
            if (condition is ProcessCondition p && string.IsNullOrWhiteSpace(p.ProcessName)) { error = "进程名不能为空。"; return false; }
            if (condition is TcpPortCondition tcp && (tcp.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(tcp.Host))) { error = "TCP 主机或端口无效。"; return false; }
            if (condition is HttpCondition http && !Uri.TryCreate(http.Url, UriKind.Absolute, out _)) { error = "HTTP URL 无效。"; return false; }
            if (condition is FileExistsCondition file && string.IsNullOrWhiteSpace(file.Path)) { error = "文件路径不能为空。"; return false; }
            if (condition is WindowsServiceCondition service && string.IsNullOrWhiteSpace(service.ServiceName)) { error = "服务名不能为空。"; return false; }
            if (condition is WindowTitleCondition window && string.IsNullOrWhiteSpace(window.TitlePattern)) { error = "窗口标题不能为空。"; return false; }
        }
        error = string.Empty; return true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
