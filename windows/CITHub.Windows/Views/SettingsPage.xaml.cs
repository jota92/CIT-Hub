using System.Security.Cryptography;
using CITHub.Windows.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace CITHub.Windows.Views;

public sealed partial class SettingsPage : Page
{
    private readonly DispatcherQueueTimer _supportRefreshTimer;
    private bool _supportRefreshInProgress;

    public SettingsPage()
    {
        InitializeComponent();
        _supportRefreshTimer = DispatcherQueue.CreateTimer();
        _supportRefreshTimer.Interval = TimeSpan.FromSeconds(15);
        _supportRefreshTimer.Tick += async (_, _) => await RefreshSupportAsync();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        Load();
        _supportRefreshTimer.Start();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e) => _supportRefreshTimer.Stop();

    private void Load()
    {
        UserId.Text = LocalStore.GetString("marin-user-id");
        DarkMode.IsOn = LocalStore.GetString("dark-mode") == "true";
        ManabaMobile.IsOn = LocalStore.GetString("manaba-smartphone", "true") == "true";
        PortalMobile.IsOn = LocalStore.GetString("portal-smartphone", "true") == "true";
        PairingStatus.Text = WindowsDeviceSessionService.IsLinked
            ? $"{WindowsDeviceSessionService.UserId} と連携済みです。"
            : "この端末はまだ連携されていません。";
        _ = RestoreAuthenticatorAsync();
        _ = RefreshSupportAsync();
        _ = RefreshNoticesAsync();
    }

    private void OnSaveCredentials(object sender, RoutedEventArgs e)
    {
        var userId = UserId.Text.Trim();
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(Password.Password)) return;
        WindowsCredentialStore.Save("marin-password", Password.Password);
        LocalStore.SetString("marin-user-id", userId);
        _ = RestoreAuthenticatorAsync();
    }

    private async void OnSaveOtp(object sender, RoutedEventArgs e)
    {
        var userId = LocalStore.GetString("marin-user-id");
        var password = Password.Password;
        if (string.IsNullOrWhiteSpace(password)) password = WindowsCredentialStore.Load("marin-password") ?? "";
        if (string.IsNullOrWhiteSpace(OtpSecret.Password) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password)) return;
        var saved = await PortalAuthenticatorSyncService.SaveAndUploadAsync(userId, password, OtpSecret.Password);
        OtpCode.Text = saved ? "Authenticator設定を保存しました" : "設定を端末に保存できませんでした";
    }

    private async void OnShowCode(object sender, RoutedEventArgs e)
    {
        if (!PortalAuthenticatorSyncService.TryLoad(out var config)) return;
        var code = Totp.Generate(config, DateTimeOffset.UtcNow);
        OtpCode.Text = code ?? "設定を確認してください";
        if (code is null) await new ContentDialog { Title = "コードを生成できません", Content = "Authenticator設定の形式を確認してください。", CloseButtonText = "閉じる", XamlRoot = XamlRoot }.ShowAsync();
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        LocalStore.SetString("dark-mode", DarkMode.IsOn ? "true" : "false");
        _ = UserPreferencesSyncService.UploadAsync();
    }

    private void OnServiceDisplayChanged(object sender, RoutedEventArgs e)
    {
        LocalStore.SetString("manaba-smartphone", ManabaMobile.IsOn ? "true" : "false");
        LocalStore.SetString("portal-smartphone", PortalMobile.IsOn ? "true" : "false");
        _ = UserPreferencesSyncService.UploadAsync();
    }

    private async void OnClaimWindowsDevice(object sender, RoutedEventArgs e)
    {
        var result = await WindowsDeviceSessionService.ClaimAsync(PairingCode.Text ?? "");
        PairingStatus.Text = result.Message;
        if (result.Success)
        {
            PairingCode.Text = "";
            await UserPreferencesSyncService.SynchronizeAsync();
            Load();
            await RestoreAuthenticatorAsync();
        }
    }

    private async Task RestoreAuthenticatorAsync()
    {
        var userId = LocalStore.GetString("marin-user-id");
        var password = WindowsCredentialStore.Load("marin-password") ?? "";
        var linkedId = WindowsDeviceSessionService.UserId;
        if (string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(linkedId)) userId = linkedId;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password)) return;
        var result = await PortalAuthenticatorSyncService.SynchronizeAsync(userId, password);
        if (result == SyncResult.Restored) OtpCode.Text = "Authenticator設定を同期しました";
    }

    private async Task RefreshSupportAsync()
    {
        if (!WindowsDeviceSessionService.IsLinked) { SupportStatus.Text = "端末連携後に利用できます。"; return; }
        if (_supportRefreshInProgress) return;

        _supportRefreshInProgress = true;
        try
        {
            var messages = await SupportChatService.LoadAsync();
            SupportMessages.ItemsSource = messages.Select(message => message.Display).ToList();
            SupportStatus.Text = messages.Count == 0 ? "メッセージはありません。" : "最新の会話を表示しています。";
        }
        finally
        {
            _supportRefreshInProgress = false;
        }
    }

    private async void OnRefreshSupport(object sender, RoutedEventArgs e) => await RefreshSupportAsync();

    private async void OnSendSupport(object sender, RoutedEventArgs e)
    {
        var sent = await SupportChatService.SendAsync(SupportDraft.Text);
        if (sent is null) { SupportStatus.Text = "送信できませんでした。端末連携と通信を確認してください。"; return; }
        SupportDraft.Text = "";
        await RefreshSupportAsync();
    }

    private async Task RefreshNoticesAsync()
    {
        if (!WindowsDeviceSessionService.IsLinked) { NoticeStatus.Text = "端末連携後にお知らせを取得できます。"; return; }
        var notices = await AdminNotificationService.LoadAsync();
        NoticeList.ItemsSource = notices;
        NoticeStatus.Text = notices.Count == 0 ? "現在のお知らせはありません。" : $"{notices.Count}件のお知らせがあります。";
    }

    private async void OnRefreshNotices(object sender, RoutedEventArgs e) => await RefreshNoticesAsync();

    private async void OnNoticeSelected(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AdminNotice notice && Uri.TryCreate(notice.url, UriKind.Absolute, out var url)) await Launcher.LaunchUriAsync(url);
    }
}

internal static class Totp
{
    public static string? Generate(PortalAuthenticatorConfig config, DateTimeOffset now)
    {
        try
        {
            var key = DecodeBase32(config.secretBase32);
            var counter = BitConverter.GetBytes(now.ToUnixTimeSeconds() / config.period);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);
            using HMAC hmac = config.algorithm switch { "SHA256" => new HMACSHA256(key), "SHA512" => new HMACSHA512(key), _ => new HMACSHA1(key) };
            var hash = hmac.ComputeHash(counter);
            var offset = hash[^1] & 0x0f;
            var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
            return (binary % (int)Math.Pow(10, config.digits)).ToString($"D{config.digits}");
        }
        catch { return null; }
    }
    private static byte[] DecodeBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0; var value = 0; var output = new List<byte>();
        foreach (var character in input.ToUpperInvariant().Replace("=", string.Empty))
        {
            var index = alphabet.IndexOf(character); if (index < 0) throw new FormatException();
            value = (value << 5) | index; bits += 5;
            if (bits < 8) continue;
            output.Add((byte)((value >> (bits - 8)) & 0xff)); bits -= 8;
        }
        return output.ToArray();
    }
}
