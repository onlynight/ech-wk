using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ECHWorkers.WinUI3.Converters;

/// <summary>
/// 布尔值通用转换器：单个实例按参数在三种模式间切换。
/// 参数 "invert" → bool 取反（用于 IsEnabled 绑定 IsRunning）；
/// 参数 "dim" → 运行中降透明度（double）；
/// 缺省 → bool 转 Visibility。
/// </summary>
public sealed class BoolConverter : IValueConverter
{
    public static BoolConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        return parameter switch
        {
            "invert" => !flag,
            "dim" => flag ? 0.55 : 1.0,
            _ => flag ? Visibility.Visible : Visibility.Collapsed,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? b : false;
}
