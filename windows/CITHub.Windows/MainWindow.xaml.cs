using CITHub.Windows.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CITHub.Windows;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(TimetablePage));
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag) return;
        var page = tag switch
        {
            "timetable" => typeof(TimetablePage),
            "todo" => typeof(TodoPage),
            "services" => typeof(ServicesPage),
            "board" => typeof(BoardPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(TimetablePage)
        };
        if (ContentFrame.CurrentSourcePageType != page) ContentFrame.Navigate(page);
    }

    public void Navigate(Type page, object? parameter = null)
    {
        if (ContentFrame.CurrentSourcePageType != page || parameter is not null)
        {
            ContentFrame.Navigate(page, parameter);
        }
    }
}
