using System.Net.Http.Json;
using System.Security.Cryptography;
using Windows.Security.Credentials;

namespace CITHub.Windows.Services;

public static class WindowsDeviceSessionService
{
    private const string ApiBaseUrl = "https://cithub-api.johta0902.workers.dev";
    private const string VaultResource = "CITHub.Windows.DeviceSession";
    private const string SessionKey = "session";
    private const string UserIdKey = "user-id";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static string DeviceToken
    {
        get
        {
            var existing = LocalStore.GetString("windows-device-token");
            if (existing.Length == 64) return existing;
            var bytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToHexString(bytes).ToLowerInvariant();
            LocalStore.SetString("windows-device-token", token);
            return token;
        }
    }

    public static string? UserId => ReadSecret(UserIdKey);
    public static bool IsLinked => !string.IsNullOrWhiteSpace(UserId) && !string.IsNullOrWhiteSpace(ReadSecret(SessionKey));

    public static async Task<EnrollmentResult> ClaimAsync(string code)
    {
        var normalizedCode = code.Trim();
        if (string.IsNullOrEmpty(normalizedCode)) return EnrollmentResult.Failed("連携コードを入力してください。");

        var request = new
        {
            code = normalizedCode,
            device_token = DeviceToken,
            bundle_id = "com.jota.cithub.windows",
            app_version = "2.0.1",
            device = "Windows",
            info = "native-winui"
        };

        try
        {
            using var response = await Client.PostAsJsonAsync($"{ApiBaseUrl}?endpoint=windows-enrollment-claim", request);
            var payload = await response.Content.ReadFromJsonAsync<EnrollmentResponse>();
            if (!response.IsSuccessStatusCode || payload?.success != true || string.IsNullOrWhiteSpace(payload.user_id) || string.IsNullOrWhiteSpace(payload.device_session))
                return EnrollmentResult.Failed(payload?.error == "invalid_or_expired_code" ? "連携コードの有効期限が切れているか、すでに使用されています。" : "Windows端末を連携できませんでした。");

            SaveSecret(UserIdKey, payload.user_id);
            SaveSecret(SessionKey, payload.device_session);
            return EnrollmentResult.Succeeded(payload.user_id);
        }
        catch
        {
            return EnrollmentResult.Failed("通信を確認して、もう一度実行してください。");
        }
    }

    public static Dictionary<string, string>? AuthorizedBody()
    {
        var userId = UserId;
        var session = ReadSecret(SessionKey);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(session)) return null;
        return new Dictionary<string, string>
        {
            ["id"] = userId,
            ["platform"] = "windows",
            ["device_token"] = DeviceToken,
            ["device_session"] = session
        };
    }

    private static void SaveSecret(string key, string value)
    {
        var vault = new PasswordVault();
        try { vault.Remove(vault.Retrieve(VaultResource, key)); } catch { }
        vault.Add(new PasswordCredential(VaultResource, key, value));
    }

    private static string? ReadSecret(string key)
    {
        try
        {
            var credential = new PasswordVault().Retrieve(VaultResource, key);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch { return null; }
    }

    private sealed class EnrollmentResponse
    {
        public bool? success { get; set; }
        public string? error { get; set; }
        public string? user_id { get; set; }
        public string? device_session { get; set; }
    }
}

public sealed record EnrollmentResult(bool Success, string Message)
{
    public static EnrollmentResult Succeeded(string userId) => new(true, $"{userId} と連携しました。");
    public static EnrollmentResult Failed(string message) => new(false, message);
}
