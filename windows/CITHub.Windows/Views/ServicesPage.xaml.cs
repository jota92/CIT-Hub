using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Storage;
using Windows.System;

namespace CITHub.Windows.Views;

public sealed partial class ServicesPage : Page
{
    private static readonly Dictionary<string, Uri> ServiceUrls = new()
    {
        ["manaba"] = new("https://cit.manaba.jp/"),
        ["portal"] = new("https://portal.chibatech.ac.jp/uprx/MobileShibbolethAuthServlet"),
        ["cafeteria"] = new("https://www.cit-s.com/wp/wp-content/themes/cit/syokudo/s1.pdf"),
        ["bus"] = new("https://www.it-chiba.ac.jp/campuslife/bus/"),
        ["calendar"] = new("https://kmsk.is.it-chiba.ac.jp/portal/whole/gakubu/gakunenreki.pdf")
    };
    private string _requestedService = "manaba";
    private Uri? _currentUri;
    private bool _isBrowserReady;

    public ServicesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeBrowserAsync();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string tab && ServiceUrls.ContainsKey(tab))
        {
            _requestedService = tab;
            if (_isBrowserReady) Navigate(tab);
        }
    }

    private async Task InitializeBrowserAsync()
    {
        if (_isBrowserReady) return;
        BrowserLoading.IsActive = true;
        ServiceStatus.Text = "ブラウザを準備しています";
        var userDataFolder = Path.Combine(ApplicationData.Current.LocalFolder.Path, "webview-profile");
        var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, null);
        await Browser.EnsureCoreWebView2Async(environment);
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        Browser.CoreWebView2.NavigationStarting += (_, args) =>
        {
            BrowserLoading.IsActive = true;
            ServiceStatus.Text = "ページを読み込んでいます";
            _currentUri = new Uri(args.Uri);
        };
        Browser.CoreWebView2.NavigationCompleted += (_, args) =>
        {
            BrowserLoading.IsActive = false;
            ServiceStatus.Text = args.IsSuccess ? "表示中" : "読み込みに失敗しました。再読み込みしてください。";
        };
        Browser.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            Browser.CoreWebView2.Navigate(args.Uri);
        };
        _isBrowserReady = true;
        Navigate(_requestedService);
    }

    private void Navigate(string tab)
    {
        _requestedService = tab;
        if (!_isBrowserReady || Browser.CoreWebView2 is null || !ServiceUrls.TryGetValue(tab, out var url)) return;
        BrowserLoading.IsActive = true;
        ServiceStatus.Text = "ページを読み込んでいます";
        _currentUri = url;
        Browser.Source = url;
    }
    private void OnServiceSelected(object sender, RoutedEventArgs e) => Navigate((sender as FrameworkElement)?.Tag as string ?? "manaba");
    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack) Browser.GoBack();
    }
    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (_isBrowserReady) Browser.Reload();
    }
    private async void OnOpenExternal(object sender, RoutedEventArgs e)
    {
        if (_currentUri is not null) await Launcher.LaunchUriAsync(_currentUri);
    }
}
