using ECHWorkers.WinUI3.Services;
using Microsoft.UI.Xaml;

namespace ECHWorkers.WinUI3;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ServerConfigService.Load();

        var mainWindow = new MainWindow();
        mainWindow.Closed += (_, _) =>
        {
            SystemProxyService.Disable();
            Application.Current?.Exit();
        };
        mainWindow.Activate();
    }
}
