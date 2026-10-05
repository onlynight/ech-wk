using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ECHWorkers.WinUI3.Models;

/// <summary>日志严重级别，决定条目前景色。</summary>
public enum LogSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// 单条运行日志。除 Brush 外均为纯数据，可在后台线程（进程输出回调）安全构造；
/// Brush 是带线程亲和性的 DependencyObject，只在 UI 线程 x:Bind 求值时创建并按主题缓存。
/// </summary>
public sealed class LogEntry
{
    private static Brush? _infoBrushDark;
    private static Brush? _infoBrushLight;
    private static Brush? _warnBrushDark;
    private static Brush? _warnBrushLight;
    private static Brush? _errorBrushDark;
    private static Brush? _errorBrushLight;

    private LogEntry(string display, LogSeverity severity)
    {
        Display = display;
        Severity = severity;
    }

    /// <summary>展示文本：[HH:mm:ss] 消息。</summary>
    public string Display { get; }

    public LogSeverity Severity { get; }

    /// <summary>
    /// 按级别取前景画刷。必须始终返回有效画刷：
    /// 绑定求值为 null 等同于 x:Null，会以本地值覆盖 TextBlock 默认样式的主题前景色，
    /// 导致文字完全不可见（背景可见、滚动条正常但内容空白）。
    /// </summary>
    public Brush Brush
    {
        get
        {
            var isDark = Application.Current?.RequestedTheme != ApplicationTheme.Light;
            return Severity switch
            {
                LogSeverity.Error => Obtain(ref _errorBrushDark, ref _errorBrushLight, isDark, 0xFFF87171, 0xFFDC2626),
                LogSeverity.Warning => Obtain(ref _warnBrushDark, ref _warnBrushLight, isDark, 0xFFFBBF24, 0xFFB45309),
                _ => Obtain(ref _infoBrushDark, ref _infoBrushLight, isDark, 0xFFFFFFFF, 0xFF000000),
            };
        }
    }

    /// <summary>在产生日志的线程上调用（可为后台线程），时间戳取入队时刻。</summary>
    public static LogEntry Create(string message)
    {
        return new LogEntry($"[{DateTime.Now:HH:mm:ss}] {message}", DetectSeverity(message));
    }

    private static LogSeverity DetectSeverity(string message)
    {
        if (message.StartsWith("[错误]", StringComparison.Ordinal)) return LogSeverity.Error;
        if (message.StartsWith("[警告]", StringComparison.Ordinal)) return LogSeverity.Warning;
        return LogSeverity.Info;
    }

    private static Brush Obtain(ref Brush? dark, ref Brush? light, bool isDark, uint darkArgb, uint lightArgb)
    {
        if (isDark)
        {
            return dark ??= MakeBrush(darkArgb);
        }
        return light ??= MakeBrush(lightArgb);
    }

    private static Brush MakeBrush(uint argb) => new SolidColorBrush(
        Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
}
