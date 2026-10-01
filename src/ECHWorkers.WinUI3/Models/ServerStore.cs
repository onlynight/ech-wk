using System.ComponentModel;
using System.Collections.ObjectModel;

namespace ECHWorkers.WinUI3.Models;

/// <summary>
/// 全局服务器配置存储，首页与服务器页面共享同一份列表。
/// 当前仅承载 UI 编辑所需字段，持久化与真实连接后续补齐。
/// </summary>
public static class ServerStore
{
    private static string _currentServerId = "";

    public static ObservableCollection<ServerProfile> Servers { get; } = new();
    public static string CurrentServerId
    {
        get => _currentServerId;
        set
        {
            _currentServerId = value;
            Changed?.Invoke(null, new PropertyChangedEventArgs(nameof(CurrentServerId)));
        }
    }

    public static event PropertyChangedEventHandler? Changed;

    static ServerStore()
    {
        Servers.CollectionChanged += (_, _) => Changed?.Invoke(null, new PropertyChangedEventArgs(nameof(Servers)));
    }

    public static void NotifyChanged() => Changed?.Invoke(null, new PropertyChangedEventArgs(nameof(Servers)));
}
