using System.Collections.ObjectModel;
using System.ComponentModel;
using ECHWorkers.WinUI3.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ECHWorkers.WinUI3.Services;

/// <summary>
/// 首页状态与代理进程生命周期的持有者。
///
/// 关键约束：本实例由 App 静态持有，不能与首页（HomePage）绑定寿命。
/// MainWindow 导航时页面无保存地被销毁重建，若把 _running / ProxyProcessService 放在
/// HomePage 里，"首页 → 服务器 → 首页"的导航就会把运行态清零（服务器页解锁编辑），
/// 而代理进程仍在跑。把状态提到这里后，页面重建只影响 UI，不影响进程与运行标记。
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private static MainViewModel? _instance;

    /// <summary>全局唯一实例；App 退出时负责停止代理进程。</summary>
    public static MainViewModel Instance => _instance ??= new();

    public static MainViewModel InstanceOrThrow => _instance ?? throw new InvalidOperationException(
        "MainViewModel 尚未初始化");

    public MainViewModel()
    {
        ProxyService.LogReceived += OnProxyLog;
        ProxyService.Exited += OnProxyExited;

        // 记录构造时（UI 线程）的调度器。子进程 Exited 事件在后台线程触发，
        // 那里没有调度器，GetForCurrentThread 会抛异常导致回调丢失、状态卡死。
        _uiDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    }

    // ============ 进程与运行态 ============

    public ProxyProcessService ProxyService { get; } = new();

    private bool _running;
    /// <summary>代理进程是否正在运行。</summary>
    public bool IsRunning
    {
        get => _running;
        private set => Set(ref _running, value, nameof(IsRunning), nameof(CanStart), nameof(CanStop));
    }

    private bool _canStart = true;
    /// <summary>能否启动（启动过程中的短暂禁用）。</summary>
    public bool CanStart { get => _canStart; set => Set(ref _canStart, value, nameof(CanStart)); }

    private bool _canStop;
    /// <summary>能否停止（仅运行中可停止）。</summary>
    public bool CanStop { get => _canStop; set => Set(ref _canStop, value, nameof(CanStop)); }

    // 非用户发起的停止：用于区分"意外退出"与"用户点了停止"
    private bool _intentionalStop;

    // ============ 当前节点 ============

    private ServerProfile? _selectedServer;
    public ServerProfile? SelectedServer
    {
        get => _selectedServer;
        set => Set(ref _selectedServer, value, nameof(SelectedServer), nameof(NodeServer), nameof(NodeName),
                   nameof(ListenText));
    }

    public string NodeServer => _selectedServer?.Server ?? "domain:port";
    public string NodeName => _selectedServer?.Name ?? "未选择服务器";

    // ============ UI 显示值（供首页绑定） ============

    private string _statusText = "已停止";
    public string StatusText { get => _statusText; set => Set(ref _statusText, value, nameof(StatusText)); }

    private SolidColorBrush _statusDotFill = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
    public SolidColorBrush StatusDotFill
    {
        get => _statusDotFill;
        set => Set(ref _statusDotFill, value, nameof(StatusDotFill));
    }

    private string _listenText = "127.0.0.1:30000";
    public string ListenText
    {
        get => _listenText;
        set => Set(ref _listenText, value, nameof(ListenText));
    }

    private string _startText = "启动";
    public string StartText { get => _startText; set => Set(ref _startText, value, nameof(StartText)); }

    private string _startGlyph = "\uE768"; // Segoe Fluent Icons: 播放
    /// <summary>Segoe Fluent Icons：播放/暂停</summary>
    public string StartGlyph
    {
        get => _startGlyph;
        set => Set(ref _startGlyph, value, nameof(StartGlyph));
    }

    // ============ 日志 ============

    private readonly object _logLock = new();
    private readonly System.Collections.Generic.Queue<LogEntry> _pendingLogs = new();

    private static readonly TimeSpan LogFlushInterval = TimeSpan.FromMilliseconds(150);
    private const int MaxLogEntries = 2000;
    private Microsoft.UI.Dispatching.DispatcherQueue? _uiDispatcher;
    private DispatcherTimer? _logFlushTimer;
    private readonly ObservableCollection<LogEntry> _uiLogs = new();

    /// <summary>供首页 ListView 绑定的日志集合。</summary>
    public ObservableCollection<LogEntry> LogItems => _uiLogs;

    // 日志滚动跟随：由首页在可见期间注册滚动目标，由本实例的定时器驱动滚动到底。
    // 必须持有引用而非依赖"当前可见控件"：长时间后台运行后页面被导航重建，
    // 新实例重新调用 AttachLogScroller 即可，滚动控制不需要常驻页面。
    private Microsoft.UI.Xaml.Controls.ListView? _logListView;
    private Microsoft.UI.Xaml.Controls.ScrollViewer? _logScrollViewer;
    private DispatcherTimer? _scrollTimer;
    private bool _logScrollAttached;

    /// <summary>
    /// 首页调用：接管日志列表的滚动控制。
    /// 每次调用都重新绑定滚动目标——页面隐藏后重新打开时控件布局可能尚未完成，
    /// 因此除立即滚动一次外，还依赖定时器在后续布局完成时继续纠正。
    /// </summary>
    public void AttachLogScroller(Microsoft.UI.Xaml.Controls.ListView list)
    {
        // 换控件时先摘掉旧事件，避免多个 ScrollViewer 同时触发
        if (_logScrollAttached && _logScrollViewer != null)
        {
            _logScrollViewer.SizeChanged -= OnLogScrollSizeChanged;
        }

        _logListView = list;
        _logScrollViewer = FindDescendant<Microsoft.UI.Xaml.Controls.ScrollViewer>(list);
        if (_logScrollViewer != null)
        {
            _logScrollViewer.SizeChanged += OnLogScrollSizeChanged;
            _logScrollAttached = true;
        }

        if (_scrollTimer == null)
        {
            _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _scrollTimer.Tick += (_, _) => ScrollLogToBottom();
            _scrollTimer.Start();
        }
        ScrollLogToBottom();
    }

    /// <summary>
    /// 视图尺寸变化时（窗口重新打开、布局完成后）滚到底部。
    /// 这是重新打开窗口后能对齐最新日志的关键：重新打开时首次布局可能尚未完成，
    /// 定时器的一次尝试可能落空，布局完成后的尺寸变化会补一次滚动。
    /// </summary>
    private void OnLogScrollSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs args)
    {
        ScrollLogToBottom();
    }

    /// <summary>滚动到底部；仅在列表可见且布局已就绪时执行。</summary>
    private void ScrollLogToBottom()
    {
        var viewer = _logScrollViewer;
        if (viewer == null) return;
        var list = _logListView;
        // 页面不在可视化树中（正看其他页）时跳过，避免对已卸载控件操作
        if (list != null && Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(list) == null) return;
        // 窗口/视图尚未完成布局时 ScrollableHeight 不可靠，跳过本次等下轮
        if (viewer.ActualHeight <= 0) return;

        list?.UpdateLayout();
        viewer.ChangeView(null, viewer.ScrollableHeight, null, disableAnimation: true);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T found) return found;
            var nested = FindDescendant<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    // ============ 公共方法 ============

    /// <summary>由首页在加载时调用，确保日志定时刷新在运行。</summary>
    public void EnsureLogFlush()
    {
        if (_logFlushTimer != null) return;
        _logFlushTimer = new DispatcherTimer { Interval = LogFlushInterval };
        _logFlushTimer.Tick += (_, _) => FlushPendingLogs();
        _logFlushTimer.Start();
    }

    public ServerProfile? RefreshServerList()
    {
        var savedId = ServerStore.CurrentServerId;
        ServerProfile? selected = null;
        var idx = -1;

        for (var i = 0; i < ServerStore.Servers.Count; i++)
        {
            if (!string.IsNullOrEmpty(savedId) && ServerStore.Servers[i].Id == savedId)
            {
                idx = i;
                selected = ServerStore.Servers[i];
                break;
            }
        }

        if (selected == null && ServerStore.Servers.Count > 0)
        {
            selected = ServerStore.Servers[0];
        }

        SelectedServer = selected;
        return selected;
    }

    /// <summary>首页下拉框选中项变化时调用。</summary>
    public void OnServerSelectionChanged(ServerProfile? profile)
    {
        if (profile == null) return;
        SelectedServer = profile;
        ServerStore.CurrentServerId = profile.Id;
        ServerConfigService.Save();
    }

    public Task ToggleProxyAsync() => IsRunning ? StopProxyAsync() : StartProxyAsync();

    public async Task StartProxyAsync()
    {
        if (SelectedServer == null)
        {
            AppendLog("[错误] 请先选择服务器配置。");
            return;
        }

        CanStart = false;
        try
        {
            var started = await ProxyService.Start(SelectedServer);
            if (!started)
            {
                AppendLog("[错误] 代理服务启动失败，请检查 ech-workers.exe 是否存在。");
                return;
            }

            var listenAddr = SelectedServer.Listen;
            var portStr = listenAddr.Contains(':') ? listenAddr.Split(':')[^1] : "30000";
            var port = int.TryParse(portStr, out var p) ? p : 30000;

            // 注册表写入 + WinINET 通知会阻塞，放到线程池避免卡 UI
            var proxyOk = await Task.Run(() => SystemProxyService.Enable(port));
            AppendLog(proxyOk
                ? $"系统代理已设置：127.0.0.1:{port}"
                : "[警告] 系统代理设置失败，但代理服务已启动。");

            IsRunning = true;
            UpdateRunningUI(port);
            ServerStore.SetRunning(SelectedServer.Id);
            App.Tray?.SetStatus(isRunning: true, proxyText: $"端口 {port}");
        }
        finally
        {
            CanStart = true;
        }
    }

    public async Task StopProxyAsync()
    {
        if (!IsRunning) return;

        CanStart = false;
        _intentionalStop = true;
        try
        {
            // 清代理 + 终止进程都会阻塞（Stop 内含 WaitForExit 3 秒），统一丢到线程池
            var proxyOk = await Task.Run(() =>
            {
                var ok = SystemProxyService.Disable();
                ProxyService.Stop();
                return ok;
            });

            AppendLog(proxyOk ? "系统代理已清除。" : "[警告] 系统代理清除失败。");

            IsRunning = false;
            ServerStore.ClearRunning();
            UpdateStoppedUI();
            App.Tray?.SetStatus(isRunning: false, proxyText: "代理未启动");
        }
        finally
        {
            _intentionalStop = false;
            CanStart = true;
        }
    }

    /// <summary>应用退出兜底：清理系统代理并停止代理进程。</summary>
    public void Shutdown()
    {
        try { SystemProxyService.Disable(); } catch { }
        try { ProxyService.Stop(); } catch { }
    }

    // ============ 内部 ============

    private void UpdateRunningUI(int port)
    {
        StatusText = "运行中";
        StatusDotFill = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E));
        ListenText = $"127.0.0.1:{port}";
        StartText = "停止";
        StartGlyph = "\uE769"; // 暂停
    }

    private void UpdateStoppedUI()
    {
        StatusText = "已停止";
        StatusDotFill = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
        StartText = "启动";
        StartGlyph = "\uE768"; // 播放
    }

    private void OnProxyLog(string line)
    {
        // 后台线程回调：只做纯数据入队，不触碰任何 UI 对象
        lock (_logLock) _pendingLogs.Enqueue(LogEntry.Create(line));
    }

    private void OnProxyExited(int exitCode)
    {
        // 子进程 Exited 在后台线程触发；切回 UI 线程更新状态
        _uiDispatcher?.TryEnqueue(() =>
        {
            if (IsRunning && !_intentionalStop)
            {
                AppendLog($"[警告] 代理服务意外退出，退出码: {exitCode}");
                _ = StopProxyAsync();
            }
        });
    }

    /// <summary>状态类日志入口（启动/停止/错误提示），同样入队合流。</summary>
    public void AppendLog(string message)
    {
        lock (_logLock) _pendingLogs.Enqueue(LogEntry.Create(message));
    }

    /// <summary>每个刷新周期把积压日志一次性批量上屏。</summary>
    private void FlushPendingLogs()
    {
        List<LogEntry> batch;
        lock (_logLock)
        {
            if (_pendingLogs.Count == 0) return;
            batch = new List<LogEntry>();
            while (_pendingLogs.Count > MaxLogEntries) _pendingLogs.Dequeue();
            while (_pendingLogs.Count > 0) batch.Add(_pendingLogs.Dequeue());
        }

        foreach (var entry in batch) _uiLogs.Add(entry);

        var excess = _uiLogs.Count - MaxLogEntries;
        for (var i = 0; i < excess; i++) _uiLogs.RemoveAt(0);
    }

    private void Set<T>(ref T field, T value, params string[] propertyNames)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        foreach (var name in propertyNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
