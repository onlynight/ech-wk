using System.ComponentModel;

namespace ECHWorkers.WinUI3.Models;

/// <summary>
/// 单个服务器（节点）配置，字段与 gui.py 中的服务器配置结构一致。
/// </summary>
public class ServerProfile : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "新节点";
    private string _server = "example.com:443";
    private string _listen = "127.0.0.1:30000";
    private string _token = "";
    private string _ip = "";
    private string _dns = "dns.alidns.com/dns-query";
    private string _ech = "cloudflare-ech.com";
    private string _routing = "bypass_cn";
    private bool _isSelected = false;
    private bool _isRunning = false;

    /// <summary>列表勾选状态，仅用于 UI 批量选择，不参与持久化。</summary>
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value, nameof(IsSelected)); }

    /// <summary>
    /// 该节点是否正在运行代理。运行中配置不可编辑（进程已按启动时的配置拉起）。
    /// 由 ServerStore.SetRunning/ClearRunning 同步，并通知 UI 刷新卡片状态。
    /// </summary>
    public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value, nameof(IsRunning)); }

    public string Id { get => _id; set => Set(ref _id, value, nameof(Id)); }
    public string Name { get => _name; set => Set(ref _name, value, nameof(Name)); }
    public string Server { get => _server; set => Set(ref _server, value, nameof(Server)); }
    public string Listen { get => _listen; set => Set(ref _listen, value, nameof(Listen)); }
    public string Token { get => _token; set => Set(ref _token, value, nameof(Token)); }
    public string Ip { get => _ip; set => Set(ref _ip, value, nameof(Ip)); }
    public string Dns { get => _dns; set => Set(ref _dns, value, nameof(Dns)); }
    public string Ech { get => _ech; set => Set(ref _ech, value, nameof(Ech)); }
    public string Routing { get => _routing; set => Set(ref _routing, value, nameof(Routing)); }

    /// <summary>用于列表展示的端点摘要。</summary>
    public string Endpoint => Server;

    public ServerProfile Clone() => new()
    {
        Id = _id, Name = _name, Server = _server, Listen = _listen,
        Token = _token, Ip = _ip, Dns = _dns, Ech = _ech, Routing = _routing,
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, params string[] propertyNames)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        foreach (var name in propertyNames)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
