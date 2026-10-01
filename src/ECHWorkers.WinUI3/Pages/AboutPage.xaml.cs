using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;

namespace ECHWorkers.WinUI3.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        RuntimeText.Text = $"Windows 11 · {arch}";
    }
}
