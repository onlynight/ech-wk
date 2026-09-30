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
        ConfigureBackdrop();
        ApplyCaptionColors();

        // Window 本身不继承 FrameworkElement，主题变更钩子挂在根 Grid 上。
        ((Grid)Content).ActualThemeChanged += OnActualThemeChanged;
    }

    /// <summary>启用 Windows 11 Mica 背景，使模糊材质延伸至标题栏与整个窗口。</summary>
    private void ConfigureBackdrop()
    {
        try
        {
            // Mica 仅在系统主题下生效，必须在窗口呈现后设置。
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
        var appWindow = this.AppWindow;
        if (appWindow == null) return;
        var titleBar = appWindow.TitleBar;

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
