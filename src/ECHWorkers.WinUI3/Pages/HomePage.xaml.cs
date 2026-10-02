using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using ECHWorkers.WinUI3.Models;
using ECHWorkers.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ECHWorkers.WinUI3.Pages;

public partial class HomePage : UserControl
{
    // 日志 UI 只保留最近 MaxLogEntries 条，超出后从最旧一端淘汰
    private const int MaxLogEntries = 2000;
    private static readonly TimeSpan LogFlushInterval = TimeSpan.FromMilliseconds(150);

    private bool _running;
    private bool _intentionalStop;
    private ServerProfile? _selectedServer;
    private readonly ProxyProcessService _proxyService = new();

    // 日志走"后台排队 + 定时批量上屏"：进程输出逐行回调只入队（无 UI 开销），
    // UI 线程每个周期一次性提交一批，布局次数与日志产生速率解耦。
    private readonly ConcurrentQueue<LogEntry> _pendingLogs = new();
    private readonly ObservableCollection<LogEntry> _logs = new();
    private readonly DispatcherTimer _logFlushTimer;
    // ListView 内部的 ScrollViewer（不公开，Loaded 后从可视化树取得），
    // 用于测量与控制"跟随底部"滚动。
    private ScrollViewer? _logScroll;

    public HomePage()
    {
        InitializeComponent();
        _proxyService.LogReceived += OnProxyLog;
        _proxyService.Exited += OnProxyExited;
        // 挂到 App 静态引用，供应用退出兜底（托盘退出/关闭退出）停止代理进程
        App.Proxy = _proxyService;

        LogList.ItemsSource = _logs;
        _logFlushTimer = new DispatcherTimer { Interval = LogFlushInterval };
        _logFlushTimer.Tick += (_, _) => FlushPendingLogs();
        _logFlushTimer.Start();

        RefreshServerList();
    }

    private void RefreshServerList()
    {
        ServerCombo.ItemsSource = ServerStore.Servers;

        // 恢复上次选中的服务器
        var savedId = ServerStore.CurrentServerId;
        var idx = -1;
        for (int i = 0; i < ServerStore.Servers.Count; i++)
        {
            if (ServerStore.Servers[i].Id == savedId && !string.IsNullOrEmpty(savedId))
            {
                idx = i;
                break;
            }
        }
        if (idx < 0 && ServerStore.Servers.Count > 0) idx = 0;
        ServerCombo.SelectedIndex = idx;

        if (ServerCombo.SelectedItem is ServerProfile p)
        {
            _selectedServer = p;
            UpdateNodeInfo(p);
        }
        else if (ServerStore.Servers.Count > 0)
        {
            var first = ServerStore.Servers[0];
            _selectedServer = first;
            UpdateNodeInfo(first);
        }
    }

    private void UpdateNodeInfo(ServerProfile p)
    {
        NodeName.Text = p.Server;
        NodeDetail.Text = p.Name;
        ListenText.Text = p.Listen;
        for (int i = 0; i < RoutingCombo.Items.Count; i++)
        {
            if (RoutingCombo.Items[i] is ComboBoxItem ci && ci.Tag?.ToString() == p.Routing)
            {
                RoutingCombo.SelectedIndex = i;
                break;
            }
        }
    }

    private void ServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServerCombo.SelectedItem is ServerProfile p)
        {
            _selectedServer = p;
            UpdateNodeInfo(p);
            ServerStore.CurrentServerId = p.Id;
            ServerConfigService.Save();
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_running)
        {
            await StartProxy();
        }
        else
        {
            StopProxy();
        }
    }

    /// <summary>切换代理启停（供系统托盘菜单调用，与 StartButton_Click 同逻辑）。</summary>
    public void ToggleProxy()
    {
        if (!_running)
        {
            _ = StartProxy();
        }
        else
        {
            StopProxy();
        }
    }

    private async Task StartProxy()
    {
        if (_selectedServer == null)
        {
            AppendLog("[错误] 请先选择服务器配置。");
            return;
        }

        StartButton.IsEnabled = false;
        try
        {
            // 同步当前选中的分流模式到 ServerProfile
            if (RoutingCombo.SelectedItem is ComboBoxItem ci && ci.Tag is string routing)
            {
                _selectedServer.Routing = routing;
            }

            var started = await _proxyService.Start(_selectedServer);
            if (!started)
            {
                AppendLog("[错误] 代理服务启动失败，请检查 ech-workers.exe 是否存在。");
                return;
            }

            var listenAddr = _selectedServer.Listen;
            var portStr = listenAddr.Contains(':')
                ? listenAddr.Split(':')[^1]
                : "30000";
            var port = int.TryParse(portStr, out var p) ? p : 30000;

            // 设置系统代理涉及注册表写入 + WinINET 通知，放到线程池避免阻塞 UI
            var proxyOk = await Task.Run(() => SystemProxyService.Enable(port));
            if (proxyOk)
            {
                AppendLog($"系统代理已设置：127.0.0.1:{port}");
            }
            else
            {
                AppendLog("[警告] 系统代理设置失败，但代理服务已启动。");
            }

            _running = true;
            UpdateRunningUI(port);
            App.Tray?.SetStatus(isRunning: true, proxyText: $"端口 {port}");
        }
        finally
        {
            StartButton.IsEnabled = true;
        }
    }

    private async void StopProxy()
    {
        StartButton.IsEnabled = false;
        _intentionalStop = true;

        // 清代理 + 终止进程都会阻塞（Stop 内含 WaitForExit 3 秒），统一丢到线程池
        var result = await Task.Run(() =>
        {
            var proxyOk = SystemProxyService.Disable();
            _proxyService.Stop();
            return proxyOk;
        });

        if (result)
        {
            AppendLog("系统代理已清除。");
        }
        else
        {
            AppendLog("[警告] 系统代理清除失败。");
        }

        _running = false;
        UpdateStoppedUI();
        StartButton.IsEnabled = true;
        App.Tray?.SetStatus(isRunning: false, proxyText: "代理未启动");
        _intentionalStop = false;
    }

    private void OnProxyLog(string line)
    {
        // 后台线程回调：只做纯数据入队，不触碰任何 UI 对象
        _pendingLogs.Enqueue(LogEntry.Create(line));
    }

    private void OnProxyExited(int exitCode)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_running && !_intentionalStop)
            {
                AppendLog($"[警告] 代理服务意外退出，退出码: {exitCode}");
                StopProxy();
            }
        });
    }

    private void UpdateRunningUI(int port)
    {
        StartText.Text = "停止";
        StartGlyph.Glyph = "\uE769"; // Segoe Fluent Icons: 暂停（与初始启动图标 &#xE768; 配对）
        StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E));
        StatusText.Text = "运行中";
        ListenText.Text = $"127.0.0.1:{port}";
        // 运行中锁定配置：进程已按启动时的配置拉起，此时改动不会生效
        ServerCombo.IsEnabled = false;
        RoutingCombo.IsEnabled = false;
    }

    private void UpdateStoppedUI()
    {
        StartText.Text = "启动";
        StartGlyph.Glyph = "\uE768"; // Segoe Fluent Icons: 播放
        StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
        StatusText.Text = "已停止";
        ServerCombo.IsEnabled = true;
        RoutingCombo.IsEnabled = true;
    }

    /// <summary>状态类日志入口（启动/停止/错误提示），同样入队合流。</summary>
    private void AppendLog(string message)
    {
        _pendingLogs.Enqueue(LogEntry.Create(message));
    }

    /// <summary>每个刷新周期把积压日志一次性批量上屏。</summary>
    private void FlushPendingLogs()
    {
        if (_pendingLogs.IsEmpty) return;

        // 先测量是否跟随底部，再添加条目：两条刷新之间只有用户会滚动，
        // 添加条目会让 ScrollableHeight 变化，添加后测量会误判。
        var pinned = IsLogAtBottom();

        // 单次突发远超展示上限时，先丢弃最旧的积压（反正 UI 只保留最近 MaxLogEntries 条）
        while (_pendingLogs.Count > MaxLogEntries)
        {
            _pendingLogs.TryDequeue(out _);
        }

        while (_pendingLogs.TryDequeue(out var entry))
        {
            _logs.Add(entry);
        }

        // 超出上限从最旧一端淘汰，内存与布局成本保持恒定
        var excess = _logs.Count - MaxLogEntries;
        for (var i = 0; i < excess; i++)
        {
            _logs.RemoveAt(0);
        }

        if (pinned)
        {
            ScrollLogToBottom();
        }
    }

    private bool IsLogAtBottom()
    {
        return _logScroll == null
            || _logScroll.ScrollableHeight <= 0
            || _logScroll.VerticalOffset >= _logScroll.ScrollableHeight - 32;
    }

    private void ScrollLogToBottom()
    {
        if (_logScroll == null) return;
        // 页面不在可视化树中（正看其他页）时无需滚动
        if (VisualTreeHelper.GetParent(LogList) == null) return;
        // 先强制完成一次布局，拿到包含新条目的完整滚动范围
        LogList.UpdateLayout();
        _logScroll.ChangeView(null, _logScroll.ScrollableHeight, null, disableAnimation: true);
    }

    private void LogList_Loaded(object sender, RoutedEventArgs e)
    {
        _logScroll = FindDescendant<ScrollViewer>(LogList);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) return found;
            var nested = FindDescendant<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private void PrimaryBtn_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button b)
        {
            b.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x3C));
            b.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA));
        }
    }

    private void PrimaryBtn_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button b)
        {
            b.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1C, 0x1C, 0x1E));
            b.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFA));
        }
    }
}
