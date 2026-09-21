using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CITHub.Windows.Services;

// Uses the same encrypted envelope and HKDF parameters as the Apple apps.
// The server stores only this ciphertext; the MARINE password never leaves this device.
public static class PortalAuthenticatorSyncService
{
    private const string ApiBaseUrl = "https://cithub-api.johta0902.workers.dev";
    private const string ConfigKey = "portal-authenticator";
    private const string RevisionKey = "portal-authenticator-revision";
    private const string UpdatedAtKey = "portal-authenticator-updated-at";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<SyncResult> SynchronizeAsync(string userId, string password)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password) || !WindowsDeviceSessionService.IsLinked)
            return SyncResult.NotReady;

        var remote = await RequestAsync(userId, "get");
        if (remote is null) return SyncResult.Failed;

        var localRaw = WindowsCredentialStore.Load(ConfigKey);
        var localDate = ParseDate(LocalStore.GetString(UpdatedAtKey));
        var remoteDate = ParseDate(remote.updated_at);
        if (remote.status == "found" && !string.IsNullOrWhiteSpace(remote.encrypted_payload) &&
            remoteDate >= localDate && TryDecrypt(remote.encrypted_payload, userId, password, out var restored))
        {
            WindowsCredentialStore.Save(ConfigKey, JsonSerializer.Serialize(restored, JsonOptions));
            LocalStore.SetString(UpdatedAtKey, remote.updated_at ?? DateTimeOffset.UtcNow.ToString("O"));
            LocalStore.SetString(RevisionKey, (remote.revision ?? 0).ToString());
            return SyncResult.Restored;
        }

        if (!string.IsNullOrWhiteSpace(localRaw) && TryParse(localRaw, out var local))
        {
            var response = await PutAsync(userId, password, local, localDate);
            return response?.status is "updated" or "kept_newer" ? SyncResult.Uploaded : SyncResult.Failed;
        }
        return SyncResult.NoRemoteConfiguration;
    }

    public static async Task<bool> SaveAndUploadAsync(string userId, string password, string rawValue)
    {
        if (!TryParse(rawValue, out var config)) return false;
        WindowsCredentialStore.Save(ConfigKey, JsonSerializer.Serialize(config, JsonOptions));
        var now = DateTimeOffset.UtcNow;
        LocalStore.SetString(UpdatedAtKey, now.ToString("O"));
        var response = await PutAsync(userId, password, config, now);
        return response?.status is "updated" or "kept_newer";
    }

    public static bool TryLoad(out PortalAuthenticatorConfig config)
    {
        var raw = WindowsCredentialStore.Load(ConfigKey);
        return !string.IsNullOrWhiteSpace(raw) && TryParse(raw, out config);
    }

    private static async Task<ApiResponse?> PutAsync(string userId, string password, PortalAuthenticatorConfig config, DateTimeOffset updatedAt)
    {
        if (!TryEncrypt(config, userId, password, out var encrypted)) return null;
        var body = WindowsDeviceSessionService.AuthorizedBody();
        if (body is null) return null;
        body["operation"] = "put";
        body["intent"] = "sync";
        body["base_revision"] = LocalStore.GetString(RevisionKey, "0");
        body["updated_at"] = updatedAt.ToString("O");
        body["encrypted_payload"] = encrypted;
        var response = await PostAsync(body);
        if (response?.revision is int revision) LocalStore.SetString(RevisionKey, revision.ToString());
        if (!string.IsNullOrWhiteSpace(response?.updated_at)) LocalStore.SetString(UpdatedAtKey, response.updated_at);
        return response;
    }

    private static Task<ApiResponse?> RequestAsync(string userId, string operation)
    {
        var body = WindowsDeviceSessionService.AuthorizedBody();
        if (body is null || !body.TryGetValue("id", out var linkedId) || !string.Equals(linkedId, userId, StringComparison.OrdinalIgnoreCase)) return Task.FromResult<ApiResponse?>(null);
        body["operation"] = operation;
        return PostAsync(body);
    }

    private static async Task<ApiResponse?> PostAsync(Dictionary<string, string> body)
    {
        try
        {
            using var response = await Client.PostAsJsonAsync($"{ApiBaseUrl}?endpoint=portal-authenticator-backup", body);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ApiResponse>();
        }
        catch { return null; }
    }

    private static bool TryParse(string raw, out PortalAuthenticatorConfig config)
    {
        config = new();
        var value = raw.Trim();
        if (value.StartsWith("{", StringComparison.Ordinal) && JsonSerializer.Deserialize<PortalAuthenticatorConfig>(value, JsonOptions) is { } json && Validate(json)) { config = json; return true; }
        var secret = value;
        var digits = 6;
        var period = 30;
        var algorithm = "SHA1";
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme.Equals("otpauth", StringComparison.OrdinalIgnoreCase))
        {
            var query = ParseQuery(uri.Query);
            secret = query.GetValueOrDefault("secret", "");
            _ = int.TryParse(query.GetValueOrDefault("digits"), out digits);
            if (digits == 0) digits = 6;
            _ = int.TryParse(query.GetValueOrDefault("period"), out period);
            if (period == 0) period = 30;
            algorithm = query.GetValueOrDefault("algorithm", "SHA1");
        }
        config = new PortalAuthenticatorConfig { secretBase32 = NormalizeSecret(secret), digits = digits, period = period, algorithm = algorithm.ToUpperInvariant() };
        return Validate(config);
    }

    private static bool Validate(PortalAuthenticatorConfig config) =>
        config.secretBase32.Length >= 4 && config.digits is >= 4 and <= 10 && config.period is >= 5 and <= 120 &&
        config.algorithm is "SHA1" or "SHA256" or "SHA512" && config.secretBase32.All(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(c));
    private static string NormalizeSecret(string raw) => new(raw.ToUpperInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray()).Replace("=", "");
    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(pair => pair.Split('=', 2)).Where(pair => pair.Length == 2)
        .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]), StringComparer.OrdinalIgnoreCase);

    private static bool TryEncrypt(PortalAuthenticatorConfig config, string userId, string password, out string envelope)
    {
        envelope = "";
        try
        {
            var salt = RandomNumberGenerator.GetBytes(16); var nonce = RandomNumberGenerator.GetBytes(12);
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(config, JsonOptions); var ciphertext = new byte[plaintext.Length]; var tag = new byte[16];
            using var aes = new AesGcm(DeriveKey(userId, password, salt), 16); aes.Encrypt(nonce, plaintext, ciphertext, tag);
            envelope = JsonSerializer.Serialize(new Envelope { version = 1, salt = Convert.ToBase64String(salt), nonce = Convert.ToBase64String(nonce), ciphertext = Convert.ToBase64String(ciphertext.Concat(tag).ToArray()) }, JsonOptions);
            return true;
        }
        catch { return false; }
    }

    private static bool TryDecrypt(string raw, string userId, string password, out PortalAuthenticatorConfig config)
    {
        config = new();
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(raw, JsonOptions);
            if (envelope is null || envelope.version != 1) return false;
            var salt = Convert.FromBase64String(envelope.salt); var nonce = Convert.FromBase64String(envelope.nonce); var combined = Convert.FromBase64String(envelope.ciphertext);
            if (combined.Length <= 16) return false;
            var plaintext = new byte[combined.Length - 16];
            using var aes = new AesGcm(DeriveKey(userId, password, salt), 16); aes.Decrypt(nonce, combined[..^16], combined[^16..], plaintext);
            var parsed = JsonSerializer.Deserialize<PortalAuthenticatorConfig>(plaintext, JsonOptions);
            if (parsed is null || !Validate(parsed)) return false; config = parsed; return true;
        }
        catch { return false; }
    }

    private static byte[] DeriveKey(string userId, string password, byte[] salt) => HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(password), 32, salt, Encoding.UTF8.GetBytes($"CIT Hub portal authenticator v1|{userId.Trim().ToUpperInvariant()}"));
    private static DateTimeOffset ParseDate(string? value) => DateTimeOffset.TryParse(value, out var date) ? date : DateTimeOffset.MinValue;

    private sealed class Envelope { public int version { get; set; } public string salt { get; set; } = ""; public string nonce { get; set; } = ""; public string ciphertext { get; set; } = ""; }
    private sealed class ApiResponse { public bool success { get; set; } public string? status { get; set; } public string? encrypted_payload { get; set; } public string? updated_at { get; set; } public int? revision { get; set; } }
}

public sealed class PortalAuthenticatorConfig { public string secretBase32 { get; set; } = ""; public int digits { get; set; } = 6; public int period { get; set; } = 30; public string algorithm { get; set; } = "SHA1"; }
public enum SyncResult { Restored, Uploaded, NoRemoteConfiguration, Failed, NotReady }
