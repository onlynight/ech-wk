using ECHWorkers.WinUI3.Models;
using ECHWorkers.WinUI3.Services;
using Microsoft.UI.Xaml;

namespace ECHWorkers.WinUI3;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    private SystemTrayService? _tray;

    /// <summary>全局托盘服务实例，供 HomePage 等页面调用 SetStatus 更新图标状态。</summary>
    public static SystemTrayService? Tray =>
        (Current as App)?._tray;

    /// <summary>
    /// 全局代理进程服务（HomePage 创建时赋值）。
    /// 退出兜底用：应用退出时代理进程必须一并停止。
    /// </summary>
    public static ProxyProcessService? Proxy { get; set; }

    /// <summary>
    /// 启动自愈：若上次异常终止（崩溃/强杀）留下"系统代理仍开启但代理进程已死"的状态，
    /// 且系统代理端口与本应用当前配置一致（避免误伤其他代理工具），自动清除系统代理。
    /// </summary>
    private static void CleanupStaleSystemProxy()
    {
        try
        {
            if (!SystemProxyService.IsEnabled(out var proxyPort)) return;
            if (System.Diagnostics.Process.GetProcessesByName("ech-workers").Length > 0) return;

            var server = ServerStore.Servers.FirstOrDefault(s => s.Id == ServerStore.CurrentServerId)
                         ?? ServerStore.Servers.FirstOrDefault();
            var listen = server?.Listen;
            if (string.IsNullOrEmpty(listen) || !listen.Contains(':')) return;
            if (!int.TryParse(listen.Split(':')[^1], out var configuredPort)) return;

            if (proxyPort == configuredPort)
            {
                SystemProxyService.Disable();
                System.Diagnostics.Debug.WriteLine(
                    $"[启动自愈] 检测到残留系统代理 127.0.0.1:{proxyPort}（代理进程已不存在），已清除。");
            }
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ServerConfigService.Load();
        AppSettingsService.Load();
        CleanupStaleSystemProxy();

        var mainWindow = new MainWindow();

        // 创建系统托盘图标；左键双击=显示窗口，右键菜单项分别触发 打开 / 切换代理 / 退出
        try
        {
            _tray = new SystemTrayService("ECH Workers");
            _tray.IconActivated += () =>
            {
                mainWindow.AppWindow.Show();
                mainWindow.Activate();
            };
            _tray.ToggleProxyRequested += () =>
            {
                // 切到首页页并模拟点击启动/停止按钮
                mainWindow.ShowHomePage();
                mainWindow.ToggleProxyFromTray();
            };
            _tray.ExitRequested += () =>
            {
                // 清理系统代理 + 停掉代理进程后真正退出
                try { SystemProxyService.Disable(); } catch { }
                try { Proxy?.Stop(); } catch { }
                _tray?.Dispose();
                Application.Current?.Exit();
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Tray] 初始化失败: {ex}");
            // 把完整异常栈写到输出目录，便于定位托盘初始化问题
            try
            {
                var logPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                    "tray_error.log");
                System.IO.File.WriteAllText(logPath,
                    $"[Tray] 初始化失败 {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}");
            }
            catch { }
        }

        // 点击关闭按钮：根据设置决定是退到托盘还是退出
        mainWindow.AppWindow.Closing += (sender, e) =>
        {
            if (AppSettingsService.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                mainWindow.AppWindow.Hide();
            }
            else
            {
                // 完全退出：清理代理 + 停止代理进程 + 托盘图标
                try { SystemProxyService.Disable(); } catch { }
                try { Proxy?.Stop(); } catch { }
                _tray?.Dispose();
            }
        };

        mainWindow.Activate();
    }
}
