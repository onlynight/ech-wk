using ECHWorkers.WinUI3.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.UI;
using Windows.UI.WindowManagement;

namespace ECHWorkers.WinUI3;

public sealed partial class MainWindow : Window
{
    private HomePage? _homePage;
    private ServersPage? _serversPage;
    private AboutPage? _aboutPage;
    private readonly Button[] _navButtons;

    // 侧边导航状态色：选中为实心底色，悬停为更浅底色，未选中透明。
    // 颜色根据当前主题动态生成（亮色模式用深色底，暗色模式用半透明白色底）。
    private SolidColorBrush SelectedBrush =>
        Application.Current?.RequestedTheme == ApplicationTheme.Light
            ? new SolidColorBrush(Color.FromArgb(0x33, 0x1C, 0x1B, 0x1F))
            : new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private SolidColorBrush HoverBrush =>
        Application.Current?.RequestedTheme == ApplicationTheme.Light
            ? new SolidColorBrush(Color.FromArgb(0x1A, 0x1C, 0x1B, 0x1F))
            : new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush ClearBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    public MainWindow()
    {
        InitializeComponent();
        _navButtons = new[] { NavHome, NavServers, NavAbout };

        ConfigureTitleBar();
        ConfigureBackdrop();
        ConfigureSize();
        ApplyCaptionColors();

        RootGrid.ActualThemeChanged += OnActualThemeChanged;
        NavItem_Click(NavHome, null!);
    }

    /// <summary>标题栏与内容合并，Mica 延伸至整个窗口顶部。</summary>
    private void ConfigureTitleBar()
    {
        this.SetTitleBar(TitleBarStrip);
        this.ExtendsContentIntoTitleBar = true;

        var appWindow = this.AppWindow;
        if (appWindow != null)
        {
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        }
        this.Title = "ECH Workers";
    }

    /// <summary>启用 Windows 11 Mica 背景。</summary>
    private void ConfigureBackdrop()
    {
        try { this.SystemBackdrop = new MicaBackdrop(); }
        catch { /* 无桌面合成器时回退为普通窗口 */ }
    }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const int GWL_MINTRACKSIZE = -0x000C;

    private static IntPtr PackSize(int width, int height) =>
        new IntPtr(height * 0x10000 + width);

    /// <summary>窗口默认尺寸与最小尺寸限制。</summary>
    private void ConfigureSize()
    {
        var appWindow = this.AppWindow;
        if (appWindow == null) return;
        appWindow.Resize(new Windows.Graphics.SizeInt32(1056, 695));

        this.Activated += OnWindowActivated;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
    {
        this.Activated -= OnWindowActivated;

        var currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == currentProcessId && IsWindowVisible(hwnd))
            {
                SetWindowLongPtr(hwnd, GWL_MINTRACKSIZE, PackSize(1056, 695));
                return false;
            }
            return true;
        }, IntPtr.Zero);
    }

    private void OnActualThemeChanged(object sender, object e) => ApplyCaptionColors();

    /// <summary>标题栏配色对齐当前主题，背景保持透明以透出 Mica。</summary>
    private void ApplyCaptionColors()
    {
        var titleBar = this.AppWindow?.TitleBar;
        if (titleBar == null) return;

        var isLight = Application.Current?.RequestedTheme == ApplicationTheme.Light;
        var text = isLight ? Color.FromArgb(0xF2, 0x1C, 0x1B, 0x1F) : Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFB);
        var hover = isLight ? Color.FromArgb(0x14, 0, 0, 0) : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF);
        var pressed = isLight ? Color.FromArgb(0x24, 0, 0, 0) : Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF);
        var clear = Color.FromArgb(0, 0, 0, 0);

        titleBar.BackgroundColor = clear;
        titleBar.ForegroundColor = text;
        titleBar.InactiveBackgroundColor = clear;
        titleBar.InactiveForegroundColor = text;
        titleBar.ButtonBackgroundColor = clear;
        titleBar.ButtonForegroundColor = text;
        titleBar.ButtonInactiveBackgroundColor = clear;
        titleBar.ButtonInactiveForegroundColor = text;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = text;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ButtonPressedForegroundColor = text;

        RefreshNavVisuals();
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string key) return;
        ShowPage(key);
    }

    private void NavItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button) RefreshOne(button);
    }

    private void NavItem_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button) RefreshOne(button);
    }

    private string _currentPage = "home";

    /// <summary>按需创建并切换页面，避免重复构建。</summary>
    private void ShowPage(string key)
    {
        _currentPage = key;

        UIElement page = key switch
        {
            "servers" => _serversPage ??= new ServersPage(),
            "about" => _aboutPage ??= new AboutPage(),
            _ => _homePage ??= new HomePage(),
        };

        PageHost.Children.Clear();
        PageHost.Children.Add(page);

        RefreshNavVisuals();
    }

    /// <summary>刷新全部导航项外观（选中底色、悬停底色、字重）。</summary>
    private void RefreshNavVisuals()
    {
        foreach (var button in _navButtons) RefreshOne(button);
    }

    private void RefreshOne(Button button)
    {
        var isSelected = button.Tag as string == _currentPage;
        var isActive = isSelected || button.IsPointerOver;

        button.Background = isSelected ? SelectedBrush : isActive ? HoverBrush : ClearBrush;
        button.Opacity = isSelected ? 1.0 : isActive ? 1.0 : 0.8;
    }
}
