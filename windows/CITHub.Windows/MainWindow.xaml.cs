using CITHub.Windows.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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

    public void ApplyTheme(bool dark)
    {
        RootNavigation.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        SetBrush("CITHubAccentBrush", dark ? ColorHelper.FromArgb(255, 10, 132, 255) : ColorHelper.FromArgb(255, 0, 95, 175));
        SetBrush("CITHubSurfaceBrush", dark ? ColorHelper.FromArgb(255, 28, 28, 30) : ColorHelper.FromArgb(255, 255, 255, 255));
        SetBrush("CITHubBackgroundBrush", dark ? ColorHelper.FromArgb(255, 0, 0, 0) : ColorHelper.FromArgb(255, 245, 245, 247));
        SetBrush("CITHubSecondaryTextBrush", dark ? ColorHelper.FromArgb(255, 174, 174, 178) : ColorHelper.FromArgb(255, 97, 97, 102));
    }

    private static void SetBrush(string key, Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush) brush.Color = color;
    }
}
