using CITHub.Windows.Models;
using CITHub.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CITHub.Windows.Views;

public sealed partial class TimetablePage : Page
{
    private List<TimetableCourse> _courses = [];
    private string _selectedDay = "";
    public TimetablePage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadCoursesAsync();
    }

    private async Task LoadCoursesAsync()
    {
        _courses = await TimetableStore.LoadAsync();
        var days = _courses.Select(course => course.Day).Where(day => !string.IsNullOrWhiteSpace(day)).Distinct().ToList();
        if (days.Count == 0)
        {
            TimetableStatus.Text = "時間割がありません。学内サービスのポータルを開き、時間割ページで「時間割を取得」を選んでください。";
            ScheduleList.ItemsSource = Array.Empty<TimetableCourse>();
            return;
        }
        _selectedDay = days.Contains(_selectedDay) ? _selectedDay : days.First();
        DaySelector.Children.Clear();
        foreach (var day in days)
        {
            var button = new Button { Content = day, Tag = day, MinWidth = 56 };
            button.Click += OnDaySelected;
            DaySelector.Children.Add(button);
        }
        TimetableStatus.Text = $"{LocalStore.GetString("timetable-updated-at", "")}".Length > 0 ? "保存済みの時間割を表示しています。" : "時間割を表示しています。";
        ShowSelectedDay();
    }

    private void OnDaySelected(object sender, RoutedEventArgs e) { _selectedDay = (sender as FrameworkElement)?.Tag as string ?? _selectedDay; ShowSelectedDay(); }
    private void ShowSelectedDay() => ScheduleList.ItemsSource = _courses.Where(course => course.Day == _selectedDay).OrderBy(course => course.Period).ToList();

    private void OnOpenCalendar(object sender, RoutedEventArgs e) =>
        App.MainWindow.Navigate(typeof(ServicesPage), "calendar");

    private void OnOpenPortal(object sender, RoutedEventArgs e) =>
        App.MainWindow.Navigate(typeof(ServicesPage), "portal-timetable");

    private async void OnCourseSelected(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TimetableCourse course) return;
        var note = new TextBox { Text = course.Note, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, PlaceholderText = "授業メモ" };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = course.Subtitle, Opacity = 0.72 }); panel.Children.Add(note);
        var dialog = new ContentDialog { Title = course.DisplayName, Content = panel, PrimaryButtonText = "メモを保存", SecondaryButtonText = "板書", CloseButtonText = "閉じる", XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary) { course.Note = note.Text.Trim(); await TimetableStore.SaveAsync(_courses); }
        if (result == ContentDialogResult.Secondary) App.MainWindow.Navigate(typeof(BoardPage), course);
    }
}
