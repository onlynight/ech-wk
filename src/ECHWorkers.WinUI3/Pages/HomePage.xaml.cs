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
    private bool _running;
    private bool _intentionalStop;
    private ServerProfile? _selectedServer;
    private readonly ProxyProcessService _proxyService = new();

    public HomePage()
    {
        InitializeComponent();
        _proxyService.LogReceived += OnProxyLog;
        _proxyService.Exited += OnProxyExited;
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
        DispatcherQueue.TryEnqueue(() => AppendLog(line));
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
    }

    private void UpdateStoppedUI()
    {
        StartText.Text = "启动";
        StartGlyph.Glyph = "\uE768"; // Segoe Fluent Icons: 播放
        StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
        StatusText.Text = "已停止";
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        LogText.Text = string.IsNullOrEmpty(LogText.Text) ? line : LogText.Text + line;

        // 第三个参数是缩放比例：传 0 会把日志文字缩小，传 null 才表示不改变缩放
        LogScroll.ChangeView(0, double.MaxValue, null);
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
