using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CITHub.Windows.Models;

namespace CITHub.Windows.Views;

public sealed partial class BoardPage : Page
{
    private StorageFolder? _folder;
    private TimetableCourse? _course;

    public BoardPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _course = e.Parameter as TimetableCourse;
        PageTitle.Text = _course is null ? "板書" : _course.DisplayName;
        CourseSubtitle.Text = _course is null ? "時間割の授業を選ぶと、授業ごとに写真を整理できます。" : $"{_course.Day} { _course.Period }限  {_course.Subtitle}";
        _folder = null;
        _ = RefreshAsync();
    }

    private async Task<StorageFolder> GetFolderAsync()
    {
        var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync("board-photos", CreationCollisionOption.OpenIfExists);
        _folder ??= await root.CreateFolderAsync(_course?.Id ?? "unfiled", CreationCollisionOption.OpenIfExists);
        return _folder;
    }

    private async Task RefreshAsync()
    {
        var files = await (await GetFolderAsync()).GetFilesAsync();
        var images = new List<BitmapImage>();
        foreach (var file in files.Where(file => new[] { ".jpg", ".jpeg", ".png", ".heic" }.Contains(Path.GetExtension(file.Name).ToLowerInvariant())))
        {
            var image = new BitmapImage();
            await image.SetSourceAsync(await file.OpenAsync(FileAccessMode.Read));
            images.Add(image);
        }
        Photos.ItemsSource = images;
    }

    private async void OnAddPhoto(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".jpg"); picker.FileTypeFilter.Add(".jpeg"); picker.FileTypeFilter.Add(".png"); picker.FileTypeFilter.Add(".heic");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        await file.CopyAsync(await GetFolderAsync(), $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{file.Name}", NameCollisionOption.GenerateUniqueName);
        await RefreshAsync();
    }

    private async void OnOpenFolder(object sender, RoutedEventArgs e) => await Launcher.LaunchFolderAsync(await GetFolderAsync());

    private async void OnPhotoSelected(object sender, ItemClickEventArgs e)
    {
        var dialog = new ContentDialog { Title = "板書写真", Content = new Image { Source = (BitmapImage)e.ClickedItem, MaxHeight = 620, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform }, CloseButtonText = "閉じる", XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }
}
