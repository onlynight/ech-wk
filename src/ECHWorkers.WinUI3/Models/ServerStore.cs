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

    // 当前正在运行代理的节点 Id；运行中时该节点配置不得修改，否则进程实际仍用旧配置。
    // 由 HomePage 启停时写入，供服务器卡片判断是否禁用编辑。
    private static string _runningServerId = "";

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

    /// <summary>
    /// 把全局运行态同步到列表中的每个节点（含新加入的）。
    /// 导航会重建页面导致列表重新绑定，重绑后必须重新同步，否则运行中的卡片会解锁。
    /// </summary>
    public static void SyncRunning()
    {
        foreach (var profile in Servers) profile.IsRunning = profile.Id == _runningServerId;
    }

    /// <summary>标记某节点为运行中；空 Id 视为清除。会同步各节点的 IsRunning 以刷新 UI。</summary>
    public static void SetRunning(string serverId)
    {
        _runningServerId = serverId ?? "";
        SyncRunning();
        Changed?.Invoke(null, new PropertyChangedEventArgs(nameof(Servers)));
    }

    /// <summary>清除运行中标记。</summary>
    public static void ClearRunning() => SetRunning("");

    /// <summary>指定节点是否正在运行代理。</summary>
    public static bool ServerIsRunning(string serverId) =>
        !string.IsNullOrEmpty(_runningServerId) && _runningServerId == serverId;

    /// <summary>是否有任何节点正在运行代理。</summary>
    public static bool AnyRunning => !string.IsNullOrEmpty(_runningServerId);

    public static void NotifyChanged() => Changed?.Invoke(null, new PropertyChangedEventArgs(nameof(Servers)));
}
