using Microsoft.UI.Xaml;
using CITHub.Windows.Services;

namespace CITHub.Windows;

public partial class App : Application
{
    public static MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
        ForegroundNoticePoller.Start(MainWindow.DispatcherQueue);
    }
}
