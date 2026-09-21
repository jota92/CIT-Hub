using System.Collections.ObjectModel;
using CITHub.Windows.Models;
using CITHub.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CITHub.Windows.Views;

public sealed partial class TodoPage : Page
{
    private readonly ObservableCollection<TodoRow> _items = [];
    private List<PersonalTodo> _todos = [];

    public TodoPage()
    {
        InitializeComponent();
        TodoList.ItemsSource = _items;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _todos = await LocalStore.LoadTodosAsync();
        _items.Clear();
        foreach (var todo in _todos.Where(item => !item.IsCompleted).OrderBy(item => item.Deadline ?? item.NotifyAt ?? DateTimeOffset.MinValue))
            _items.Add(new TodoRow(todo));
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var title = new TextBox { PlaceholderText = "タイトル（必須）" };
        var detail = new TextBox { PlaceholderText = "詳細", AcceptsReturn = true, MinHeight = 90, TextWrapping = TextWrapping.Wrap };
        var deadline = new CalendarDatePicker { PlaceholderText = "期限（任意）" };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(title); panel.Children.Add(detail); panel.Children.Add(deadline);
        var dialog = new ContentDialog { Title = "ToDoを追加", Content = panel, PrimaryButtonText = "追加", CloseButtonText = "キャンセル", XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(title.Text)) return;
        _todos.Add(new PersonalTodo(Guid.NewGuid().ToString("N"), title.Text.Trim(), detail.Text.Trim(), deadline.Date, null, false));
        await LocalStore.SaveTodosAsync(_todos);
        await RefreshAsync();
    }

    private void OnShowAll(object sender, RoutedEventArgs e) => _ = RefreshAsync();
    private void OnShowPersonal(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async void OnTodoSelected(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TodoRow row) return;
        var dialog = new ContentDialog { Title = row.Title, Content = string.IsNullOrWhiteSpace(row.Details) ? "詳細はありません。" : row.Details, CloseButtonText = "閉じる", XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    public sealed class TodoRow(PersonalTodo todo)
    {
        public string Title => todo.Title;
        public string Details => todo.Details;
        public string DeadlineDisplay => todo.Deadline?.ToString("yyyy/MM/dd") ?? "期限なし";
    }
}
