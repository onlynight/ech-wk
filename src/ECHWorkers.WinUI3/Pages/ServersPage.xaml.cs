using ECHWorkers.WinUI3.Models;
using ECHWorkers.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ECHWorkers.WinUI3.Pages;

public partial class ServersPage : UserControl
{
    public ServersPage()
    {
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        ServerList.ItemsSource = ServerStore.Servers;
        EmptyHint.Visibility = ServerStore.Servers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectAllCheckBox.IsChecked = false;
        DeleteButton.IsEnabled = false;
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        var checkAll = SelectAllCheckBox.IsChecked == true;
        foreach (var profile in ServerStore.Servers) profile.IsSelected = checkAll;
        DeleteButton.IsEnabled = ServerStore.Servers.Count > 0 && checkAll;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ServerProfile profile) OpenEditor(profile);
    }

    private void ItemDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ServerProfile profile)
        {
            ServerStore.Servers.Remove(profile);
            ServerConfigService.Save();
            RefreshList();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = ServerStore.Servers.Where(s => s.IsSelected).ToList();
        foreach (var s in selected) ServerStore.Servers.Remove(s);
        ServerConfigService.Save();
        RefreshList();
    }

    private void OpenEditor(ServerProfile? existing)
    {
        var dialog = new AddServerDialog(existing);
        dialog.Closed += (s, e) =>
        {
            if (dialog.SaveRequested)
            {
                var profile = dialog.ResultProfile;
                if (existing == null)
                {
                    ServerStore.Servers.Add(profile);
                }
                else
                {
                    var idx = ServerStore.Servers.IndexOf(existing);
                    if (idx >= 0) ServerStore.Servers[idx] = profile;
                }
                ServerConfigService.Save();
                RefreshList();
            }
        };
        dialog.Activate();
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
