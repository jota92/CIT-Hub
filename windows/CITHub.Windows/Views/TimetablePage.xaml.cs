using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CITHub.Windows.Views;

public sealed partial class TimetablePage : Page
{
    public TimetablePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCalendarAsync();
    }

    private async Task LoadCalendarAsync()
    {
        try
        {
            var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync("Assets\\schedule.json");
            using var stream = await file.OpenStreamForReadAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var rows = json.RootElement.GetProperty("entries").EnumerateArray().Take(8)
                .Select(entry => $"{entry.GetProperty("start").GetString()}  {entry.GetProperty("title").GetString()}")
                .ToList();
            ScheduleList.ItemsSource = rows;
        }
        catch
        {
            ScheduleList.ItemsSource = new[] { "年間スケジュールを読み込めませんでした。" };
        }
    }

    private void OnOpenCalendar(object sender, RoutedEventArgs e) =>
        ((MainWindow)App.MainWindow).ContentFrame.Navigate(typeof(ServicesPage), "calendar");

    private void OnOpenPortal(object sender, RoutedEventArgs e) =>
        ((MainWindow)App.MainWindow).ContentFrame.Navigate(typeof(ServicesPage), "portal");
}
