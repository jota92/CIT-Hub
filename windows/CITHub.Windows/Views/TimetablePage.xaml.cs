using CITHub.Windows.Models;
using CITHub.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using Windows.UI;

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
        ApplySavedCourseColors();
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
    private void ShowSelectedDay() => ScheduleList.ItemsSource = _courses.Where(course => course.Day == _selectedDay).OrderBy(course => course.Period).Select(course => new CourseRow(course)).ToList();

    private void OnOpenCalendar(object sender, RoutedEventArgs e) =>
        App.MainWindow.Navigate(typeof(ServicesPage), "calendar");

    private void OnOpenPortal(object sender, RoutedEventArgs e) =>
        App.MainWindow.Navigate(typeof(ServicesPage), "portal-timetable");

    private async void OnCourseSelected(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not CourseRow row) return;
        var course = row.Course;
        var note = new TextBox { Text = course.Note, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, PlaceholderText = "授業メモ" };
        var colorPicker = new ColorPicker { Color = ParseColor(course.Color), IsAlphaEnabled = false, IsColorChannelTextInputVisible = true, IsMoreButtonVisible = false };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = course.Subtitle, Opacity = 0.72 });
        panel.Children.Add(new TextBlock { Text = "授業カラー", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(colorPicker); panel.Children.Add(note);
        var assignments = await AssignmentStore.LoadAsync();
        var matchedAssignments = assignments.Where(assignment => IsSameCourse(course.Title, assignment.Course)).ToList();
        if (matchedAssignments.Count > 0)
        {
            panel.Children.Add(new TextBlock { Text = "この授業の課題", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            foreach (var assignment in matchedAssignments)
            {
                if (Uri.TryCreate(assignment.Url, UriKind.Absolute, out var assignmentUrl))
                {
                    var assignmentButton = new Button { Content = string.IsNullOrWhiteSpace(assignment.DeadlineText) ? assignment.Title : $"{assignment.Title}  {assignment.DeadlineText}", HorizontalAlignment = HorizontalAlignment.Left };
                    assignmentButton.Click += (_, _) => App.MainWindow.Navigate(typeof(ServicesPage), new ServiceNavigationRequest("manaba", assignmentUrl));
                    panel.Children.Add(assignmentButton);
                }
            }
        }
        var dialog = new ContentDialog { Title = course.DisplayName, Content = panel, PrimaryButtonText = "メモを保存", SecondaryButtonText = "板書", CloseButtonText = "閉じる", XamlRoot = XamlRoot };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            course.Note = note.Text.Trim();
            course.Color = ToHex(colorPicker.Color);
            await TimetableStore.SaveAsync(_courses);
            SaveCourseColorPreferences();
        }
        if (result == ContentDialogResult.Secondary) App.MainWindow.Navigate(typeof(BoardPage), course);
    }

    private static bool IsSameCourse(string timetableName, string assignmentName)
    {
        static string Normalize(string value) => new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        var left = Normalize(timetableName); var right = Normalize(assignmentName);
        return left.Length >= 3 && right.Length >= 3 && (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal));
    }

    private void ApplySavedCourseColors()
    {
        try
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(LocalStore.GetString("course-colors", "{}")) ?? [];
            foreach (var course in _courses) if (saved.TryGetValue(course.Id, out var color) && !string.IsNullOrWhiteSpace(color)) course.Color = color;
        }
        catch { }
    }

    private void SaveCourseColorPreferences()
    {
        var colors = _courses.Where(course => !string.IsNullOrWhiteSpace(course.Color)).ToDictionary(course => course.Id, course => course.Color);
        LocalStore.SetString("course-colors", JsonSerializer.Serialize(colors));
        _ = UserPreferencesSyncService.UploadAsync();
    }

    private static Color ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        if (hex.Length == 6 && byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var red) && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var green) && byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var blue)) return Color.FromArgb(255, red, green, blue);
        return Color.FromArgb(255, 10, 132, 255);
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private sealed class CourseRow(TimetableCourse course)
    {
        public TimetableCourse Course => course;
        public string DisplayName => course.DisplayName;
        public string Subtitle => course.Subtitle;
        public Brush AccentBrush => new SolidColorBrush(ParseColor(course.Color));
    }
}
