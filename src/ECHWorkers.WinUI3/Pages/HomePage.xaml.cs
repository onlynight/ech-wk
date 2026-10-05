using ECHWorkers.WinUI3.Models;
using ECHWorkers.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ECHWorkers.WinUI3.Pages;

/// <summary>
/// 首页视图。所有运行状态与代理进程都放在 MainViewModel（App 级）里，
/// 本页面只负责渲染与转发交互——页面随导航销毁重建不会丢失运行态。
/// </summary>
public partial class HomePage : UserControl
{
    private readonly MainViewModel _vm;

    public HomePage()
    {
        InitializeComponent();

        _vm = MainViewModel.Instance;
        DataContext = _vm;

        LogList.ItemsSource = _vm.LogItems;
        ServerCombo.ItemsSource = ServerStore.Servers;
        _vm.EnsureLogFlush();

        // 恢复上次选中的节点
        if (_vm.RefreshServerList() is ServerProfile profile)
        {
            ServerCombo.SelectedItem = profile;
        }
    }

    private void ServerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServerCombo.SelectedItem is ServerProfile profile)
        {
            _vm.OnServerSelectionChanged(profile);
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _vm.ToggleProxyAsync();
    }

    /// <summary>切换代理启停（供系统托盘菜单调用）。</summary>
    public void ToggleProxy()
    {
        _ = _vm.ToggleProxyAsync();
    }

    /// <summary>列表就绪后交给 ViewModel 接管滚动跟随；页面隐藏/重建时会自动重新注册。</summary>
    private void LogList_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.AttachLogScroller(LogList);
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
