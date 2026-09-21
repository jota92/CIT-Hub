using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Storage;
using Windows.System;
using CITHub.Windows.Services;
using CITHub.Windows.Models;
using System.Text.Json;

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
    private bool _portalSubmissionAttempted;

    public ServicesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeBrowserAsync();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string tab && tab == "portal-timetable")
        {
            _requestedService = "portal";
            if (_isBrowserReady) Navigate("portal");
        }
        else if (e.Parameter is string tab && ServiceUrls.ContainsKey(tab))
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
        Browser.CoreWebView2.DOMContentLoaded += async (_, _) => await TryPortalSignInAsync(Browser.CoreWebView2.Source);
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
        if (tab == "portal") _portalSubmissionAttempted = false;
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

    private async void OnCaptureTimetable(object sender, RoutedEventArgs e)
    {
        if (!_isBrowserReady || _requestedService != "portal" || Browser.CoreWebView2 is null)
        {
            ServiceStatus.Text = "ポータルを開いてから時間割を取得してください。";
            return;
        }
        // Portal markup differs between terms. This extraction preserves each table cell's
        // text and link rather than depending on a fixed page identifier.
        const string script = """
            (() => {
              const clean = value => (value || '').replace(/\s+/g, ' ').trim();
              const candidates = [...document.querySelectorAll('table')].map(table => ({ table, score: table.querySelectorAll('td').length }));
              const target = candidates.sort((a,b) => b.score - a.score)[0];
              if (!target || target.score < 4) return JSON.stringify({ ok:false, reason:'timetable_table_not_found' });
              const rows = [...target.table.querySelectorAll('tr')];
              const days = [...target.table.querySelectorAll('th')].map(cell => clean(cell.innerText)).filter(text => /^[月火水木金土日](曜)?$/.test(text)).map(text => text[0]);
              const courses = [];
              rows.forEach((row, rowIndex) => {
                const cells = [...row.querySelectorAll(':scope > td')];
                if (!cells.length) return;
                const period = clean((row.querySelector('th') || {}).innerText) || String(rowIndex);
                cells.forEach((cell, index) => {
                  const title = clean(cell.innerText);
                  if (!title || /^(\-|\u2014|\u00a0)$/.test(title)) return;
                  const link = cell.querySelector('a[href]');
                  courses.push({ day: days[index] || String(index + 1), period, title, detailUrl: link ? new URL(link.getAttribute('href'), location.href).href : '' });
                });
              });
              return JSON.stringify({ ok:courses.length > 0, courses });
            })()
            """;
        try
        {
            var raw = await Browser.CoreWebView2.ExecuteScriptAsync(script);
            var json = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException();
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean()) { ServiceStatus.Text = "時間割表を見つけられませんでした。ポータルで時間割ページを開いてから実行してください。"; return; }
            var courses = root.GetProperty("courses").EnumerateArray().Select(item =>
            {
                var day = item.GetProperty("day").GetString() ?? ""; var period = item.GetProperty("period").GetString() ?? ""; var title = item.GetProperty("title").GetString() ?? "";
                return new TimetableCourse { Id = TimetableStore.CourseId(day, period, title, "", ""), Day = day, Period = period, Title = title, DetailUrl = item.TryGetProperty("detailUrl", out var link) ? link.GetString() ?? "" : "" };
            }).ToList();
            await TimetableStore.SaveAsync(courses);
            ServiceStatus.Text = $"{courses.Count}件の授業を保存しました。時間割タブで確認できます。";
        }
        catch { ServiceStatus.Text = "時間割の取得に失敗しました。ポータルの時間割表を表示して再試行してください。"; }
    }

    // The portal receives only one automatic submission for a rendered sign-in page.
    // If its markup changes or the sign-in fails, the page remains usable for manual entry.
    private async Task TryPortalSignInAsync(string rawUri)
    {
        if (_requestedService != "portal" || _portalSubmissionAttempted || !Uri.TryCreate(rawUri, UriKind.Absolute, out var uri) ||
            !uri.Host.EndsWith("chibatech.ac.jp", StringComparison.OrdinalIgnoreCase)) return;
        var userId = LocalStore.GetString("marin-user-id");
        var password = WindowsCredentialStore.Load("marin-password");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
        {
            ServiceStatus.Text = "アカウント情報を設定すると、ポータルへ自動入力できます。";
            return;
        }
        var script = """
            (() => {
              const fields = Array.from(document.querySelectorAll('input'));
              const password = fields.find(x => (x.type || '').toLowerCase() === 'password');
              const user = fields.find(x => x !== password && /user|id|login|account|username/i.test(`${x.name} ${x.id} ${x.autocomplete}`)) || fields.find(x => x !== password && ['text','email'].includes((x.type || '').toLowerCase()));
              if (!user || !password || !document.querySelector('button[type=submit], input[type=submit]')) return false;
              const set = (element, value) => { const descriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value'); descriptor.set.call(element, value); element.dispatchEvent(new Event('input', { bubbles:true })); element.dispatchEvent(new Event('change', { bubbles:true })); };
              set(user, __USER_ID__); set(password, __PASSWORD__);
              const submit = document.querySelector('button[type=submit], input[type=submit]'); submit.click(); return true;
            })()
            """.Replace("__USER_ID__", JsonSerializer.Serialize(userId), StringComparison.Ordinal)
                 .Replace("__PASSWORD__", JsonSerializer.Serialize(password), StringComparison.Ordinal);
        try
        {
            var result = await Browser.CoreWebView2.ExecuteScriptAsync(script);
            if (string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
            {
                _portalSubmissionAttempted = true;
                ServiceStatus.Text = "ポータルへログインしています";
            }
            else ServiceStatus.Text = "統合認証画面です。必要に応じて手動でログインしてください。";
        }
        catch { ServiceStatus.Text = "統合認証画面を確認できませんでした。手動でログインしてください。"; }
    }
}
