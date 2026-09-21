using System.Security.Cryptography;
using System.Text;
using CITHub.Windows.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Security.Credentials;

namespace CITHub.Windows.Views;

public sealed partial class SettingsPage : Page
{
    private const string VaultResource = "CITHub.Windows";

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        UserId.Text = LocalStore.GetString("marin-user-id");
        DarkMode.IsOn = LocalStore.GetString("dark-mode") == "true";
        PairingStatus.Text = WindowsDeviceSessionService.IsLinked
            ? $"{WindowsDeviceSessionService.UserId} と連携済みです。"
            : "この端末はまだ連携されていません。";
    }

    private void OnSaveCredentials(object sender, RoutedEventArgs e)
    {
        var userId = UserId.Text.Trim();
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(Password.Password)) return;
        SaveSecret("marin-password", Password.Password);
        LocalStore.SetString("marin-user-id", userId);
    }

    private void OnSaveOtp(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OtpSecret.Password)) SaveSecret("portal-authenticator", OtpSecret.Password.Trim());
    }

    private async void OnShowCode(object sender, RoutedEventArgs e)
    {
        var stored = LoadSecret("portal-authenticator");
        if (string.IsNullOrWhiteSpace(stored)) return;
        var code = Totp.Generate(stored, DateTimeOffset.UtcNow);
        OtpCode.Text = code ?? "設定を確認してください";
        if (code is null) await new ContentDialog { Title = "コードを生成できません", Content = "Authenticator設定の形式を確認してください。", CloseButtonText = "閉じる", XamlRoot = XamlRoot }.ShowAsync();
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e) => LocalStore.SetString("dark-mode", DarkMode.IsOn ? "true" : "false");

    private async void OnClaimWindowsDevice(object sender, RoutedEventArgs e)
    {
        var result = await WindowsDeviceSessionService.ClaimAsync(PairingCode.Text ?? "");
        PairingStatus.Text = result.Message;
        if (result.Success) PairingCode.Text = "";
    }

    private static void SaveSecret(string key, string value)
    {
        var vault = new PasswordVault();
        try { vault.Remove(vault.Retrieve(VaultResource, key)); } catch { }
        vault.Add(new PasswordCredential(VaultResource, key, value));
    }
    private static string? LoadSecret(string key)
    {
        try { var credential = new PasswordVault().Retrieve(VaultResource, key); credential.RetrievePassword(); return credential.Password; }
        catch { return null; }
    }
}

internal static class Totp
{
    public static string? Generate(string raw, DateTimeOffset now)
    {
        var secret = ExtractSecret(raw);
        if (string.IsNullOrWhiteSpace(secret)) return null;
        try
        {
            var key = DecodeBase32(secret);
            var counter = BitConverter.GetBytes(now.ToUnixTimeSeconds() / 30);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);
            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counter);
            var offset = hash[^1] & 0x0f;
            var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
            return (binary % 1_000_000).ToString("D6");
        }
        catch { return null; }
    }
    private static string ExtractSecret(string raw)
    {
        if (!raw.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase)) return raw.Replace(" ", string.Empty);
        var query = new Uri(raw).Query.TrimStart('?');
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .FirstOrDefault(pair => pair.Length == 2 && pair[0].Equals("secret", StringComparison.OrdinalIgnoreCase))?
            .ElementAtOrDefault(1) is string secret
                ? Uri.UnescapeDataString(secret)
                : "";
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
