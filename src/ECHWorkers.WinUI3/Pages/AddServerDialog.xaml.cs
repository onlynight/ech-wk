using ECHWorkers.WinUI3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.WindowManagement;

namespace ECHWorkers.WinUI3.Pages;

public sealed partial class AddServerDialog : Window
{
    private readonly ServerProfile? _existing;

    public bool SaveRequested { get; private set; }
    public ServerProfile ResultProfile { get; private set; } = new();

    public AddServerDialog(ServerProfile? existing)
    {
        InitializeComponent();
        _existing = existing;
        this.Title = existing == null ? "新增服务器" : "编辑服务器";
        ConfigureTitleBar();
        ConfigureBackdrop();
        var appWindow = this.AppWindow;
        if (appWindow != null)
        {
            const int w = 720, h = 720;
            appWindow.Resize(new SizeInt32(w, h));
            var workArea = Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea;
            var cx = workArea.X + (workArea.Width - w) / 2;
            var cy = workArea.Y + (workArea.Height - h) / 2;
            appWindow.Move(new Windows.Graphics.PointInt32(cx, cy));
        }
        Populate(existing);
    }

    private void ConfigureBackdrop()
    {
        try { this.SystemBackdrop = new MicaBackdrop(); }
        catch { }
    }

    private void ConfigureTitleBar()
    {
        this.SetTitleBar(TitleBarStrip);
        this.ExtendsContentIntoTitleBar = true;
        var appWindow = this.AppWindow;
        if (appWindow != null)
        {
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        }
    }

    private void Populate(ServerProfile? profile)
    {
        NameBox.Text = profile?.Name ?? "新节点";
        ServerBox.Text = profile?.Server ?? "example.com:443";
        ListenBox.Text = profile?.Listen ?? "127.0.0.1:30000";
        TokenBox.Text = profile?.Token ?? "";
        IpBox.Text = profile?.Ip ?? "";
        DnsBox.Text = profile?.Dns ?? "dns.alidns.com/dns-query";
        EchBox.Text = profile?.Ech ?? "cloudflare-ech.com";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var profile = _existing?.Clone() ?? new ServerProfile();
        profile.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "新节点" : NameBox.Text;
        profile.Server = string.IsNullOrWhiteSpace(ServerBox.Text) ? "example.com:443" : ServerBox.Text;
        profile.Listen = string.IsNullOrWhiteSpace(ListenBox.Text) ? "127.0.0.1:30000" : ListenBox.Text;
        profile.Token = TokenBox.Text;
        profile.Ip = IpBox.Text;
        profile.Dns = string.IsNullOrWhiteSpace(DnsBox.Text) ? "dns.alidns.com/dns-query" : DnsBox.Text;
        profile.Ech = string.IsNullOrWhiteSpace(EchBox.Text) ? "cloudflare-ech.com" : EchBox.Text;

        ResultProfile = profile;
        SaveRequested = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        SaveRequested = false;
        Close();
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
