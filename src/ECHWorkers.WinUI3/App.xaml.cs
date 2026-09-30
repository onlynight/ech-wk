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
        var mainWindow = new MainWindow();
        mainWindow.Activate();
    }
}
