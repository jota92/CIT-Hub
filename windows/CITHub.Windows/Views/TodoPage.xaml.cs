using System.Collections.ObjectModel;
using CITHub.Windows.Models;
using CITHub.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

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
        var url = new TextBox { PlaceholderText = "URL（任意）" };
        StorageFile? attachment = null;
        var attachmentName = new TextBlock { Text = "添付なし", Opacity = 0.68, VerticalAlignment = VerticalAlignment.Center };
        var chooseAttachment = new Button { Content = "添付ファイルを選択" };
        chooseAttachment.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker(); picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            attachment = await picker.PickSingleFileAsync(); attachmentName.Text = attachment?.Name ?? "添付なし";
        };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(title); panel.Children.Add(detail); panel.Children.Add(deadline); panel.Children.Add(url);
        var attachmentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; attachmentPanel.Children.Add(chooseAttachment); attachmentPanel.Children.Add(attachmentName); panel.Children.Add(attachmentPanel);
        var dialog = new ContentDialog { Title = "ToDoを追加", Content = panel, PrimaryButtonText = "追加", CloseButtonText = "キャンセル", XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(title.Text)) return;
        var id = Guid.NewGuid().ToString("N");
        var attachmentPath = attachment is null ? "" : await SaveAttachmentAsync(attachment, id);
        _todos.Add(new PersonalTodo(id, title.Text.Trim(), detail.Text.Trim(), deadline.Date, null, false, false, url.Text.Trim(), attachmentPath));
        await LocalStore.SaveTodosAsync(_todos);
        await RefreshAsync();
    }

    private void OnShowAll(object sender, RoutedEventArgs e) => _ = RefreshAsync();
    private void OnShowPersonal(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async void OnTodoSelected(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TodoRow row) return;
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(row.Details) ? "詳細はありません。" : row.Details, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(row.DeadlineDisplay)) panel.Children.Add(new TextBlock { Text = $"期限: {row.DeadlineDisplay}", Opacity = 0.72 });
        var dialog = new ContentDialog { Title = row.Title, Content = panel, CloseButtonText = "閉じる", XamlRoot = XamlRoot };
        if (Uri.TryCreate(row.Url, UriKind.Absolute, out _)) dialog.PrimaryButtonText = "URLを開く";
        if (!string.IsNullOrWhiteSpace(row.AttachmentPath) && File.Exists(row.AttachmentPath)) dialog.SecondaryButtonText = "添付を開く";
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && Uri.TryCreate(row.Url, UriKind.Absolute, out var uri)) await Launcher.LaunchUriAsync(uri);
        if (result == ContentDialogResult.Secondary && File.Exists(row.AttachmentPath)) await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(row.AttachmentPath));
    }

    private static async Task<string> SaveAttachmentAsync(StorageFile source, string todoId)
    {
        var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync("todo-files", CreationCollisionOption.OpenIfExists);
        var folder = await root.CreateFolderAsync(todoId, CreationCollisionOption.OpenIfExists);
        var copied = await source.CopyAsync(folder, source.Name, NameCollisionOption.GenerateUniqueName);
        return copied.Path;
    }

    public sealed class TodoRow(PersonalTodo todo)
    {
        public string Title => todo.Title;
        public string Details => todo.Details;
        public string DeadlineDisplay => todo.Deadline?.ToString("yyyy/MM/dd") ?? "期限なし";
        public string Url => todo.Url;
        public string AttachmentPath => todo.AttachmentPath;
    }
}
