using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.WindowManagement;

namespace ECHWorkers.WinUI3;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureTitleBar();
        ConfigureBackdrop();
        ApplyCaptionColors();

        // Window 本身不继承 FrameworkElement，主题变更钩子挂在根 Grid 上。
        ((Grid)Content).ActualThemeChanged += OnActualThemeChanged;
    }

    /// <summary>
    /// 让标题栏区域与窗口内容合并：Mica 模糊延伸至标题栏，
    /// 同时为右侧系统按钮预留空间，避免内容被遮罩。
    /// </summary>
    private void ConfigureTitleBar()
    {
        // 把标题栏区域指定为 XAML 顶部的第一行（含 logo 与标题文字的 Border）。
        this.SetTitleBar(((Grid)Content).Children[0] as UIElement);

        // 开启“内容延伸至标题栏”，Mica 背景才会延伸到标题栏区域。
        this.ExtendsContentIntoTitleBar = true;
        var appWindow = this.AppWindow;
        if (appWindow != null)
        {
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
        }
        this.Title = "ECH Workers";
    }

    /// <summary>启用 Windows 11 Mica 背景，使模糊材质延伸至标题栏与整个窗口。</summary>
    private void ConfigureBackdrop()
    {
        try
        {
            this.SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // 无桌面合成器（远程会话、Windows 10 等）时回退为普通窗口，不阻塞启动。
        }
    }

    private void OnActualThemeChanged(object sender, object e) => ApplyCaptionColors();

    /// <summary>把标题栏文字与按钮配色对齐当前主题，并保留 Mica 透出。</summary>
    private void ApplyCaptionColors()
    {
        var titleBar = this.AppWindow?.TitleBar;
        if (titleBar == null) return;

        var isLight = Application.Current?.RequestedTheme == ApplicationTheme.Light;

        // 文字色：浅色主题用近黑，深色主题用近白。
        var text = isLight ? Color.FromArgb(0xF2, 0x1C, 0x1B, 0x1F)
                           : Color.FromArgb(0xFF, 0xFA, 0xFA, 0xFB);
        // 半透明叠加：浅色主题加深、深色主题提亮，均为半透明以透出 Mica。
        var hover = isLight ? Color.FromArgb(0x14, 0x00, 0x00, 0x00)
                            : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF);
        var pressed = isLight ? Color.FromArgb(0x24, 0x00, 0x00, 0x00)
                              : Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF);
        var clear = Color.FromArgb(0x00, 0x00, 0x00, 0x00);

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
    }
}
