using ECHWorkers.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ECHWorkers.WinUI3.Pages;

public partial class SettingsPage : UserControl
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();

        _loading = true;
        StartOnLoginToggle.IsOn = AppSettingsService.StartOnLogin;
        MinimizeToTrayToggle.IsOn = AppSettingsService.MinimizeToTrayOnClose;
        _loading = false;
    }

    private void StartOnLoginToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var toggle = sender as ToggleSwitch;
        AppSettingsService.SetStartOnLogin(toggle?.IsOn == true);
    }

    private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var toggle = sender as ToggleSwitch;
        AppSettingsService.SetMinimizeToTrayOnClose(toggle?.IsOn == true);
    }
}
