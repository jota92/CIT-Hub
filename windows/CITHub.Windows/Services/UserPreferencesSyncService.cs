using System.Net.Http.Json;
using System.Text.Json;

namespace CITHub.Windows.Services;

// Shares selected preferences only; passwords and session values stay on the device.
public static class UserPreferencesSyncService
{
    private const string ApiUrl = "https://cithub-api.johta0902.workers.dev?endpoint=user-preferences-backup";
    private const string TimestampKey = "windows-preferences-updated-at";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task SynchronizeAsync()
    {
        var body = WindowsDeviceSessionService.AuthorizedBody();
        if (body is null) return;
        body["operation"] = "get";
        var remote = await PostAsync(body);
        var localTimestamp = Parse(LocalStore.GetString(TimestampKey));
        if (!string.IsNullOrWhiteSpace(remote?.payload) && Parse(remote.updated_at) >= localTimestamp)
        {
            Apply(remote.payload);
            LocalStore.SetString(TimestampKey, remote.updated_at ?? DateTimeOffset.UtcNow.ToString("O"));
            return;
        }
        await UploadAsync(localTimestamp == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : localTimestamp);
    }

    public static async Task UploadAsync(DateTimeOffset? changedAt = null)
    {
        var body = WindowsDeviceSessionService.AuthorizedBody();
        if (body is null) return;
        var timestamp = changedAt ?? DateTimeOffset.UtcNow;
        body["operation"] = "put";
        body["updated_at"] = timestamp.ToString("O");
        body["payload"] = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["version"] = 1,
            ["theme_accent_hex"] = LocalStore.GetString("theme-accent", "#4A90D9"),
            ["manaba_smartphone"] = LocalStore.GetString("manaba-smartphone", "true") == "true",
            ["portal_smartphone"] = LocalStore.GetString("portal-smartphone", "true") == "true",
            ["term_preference"] = LocalStore.GetString("term-preference", "automatic"),
            ["service_tabs"] = JsonSerializer.Deserialize<JsonElement>(LocalStore.GetString("service-tabs", "[]")),
            ["course_colors"] = JsonSerializer.Deserialize<JsonElement>(LocalStore.GetString("course-colors", "{}")),
            ["course_names"] = JsonSerializer.Deserialize<JsonElement>(LocalStore.GetString("course-names", "{}")),
            ["course_notes"] = JsonSerializer.Deserialize<JsonElement>(LocalStore.GetString("course-notes", "{}")),
        });
        var response = await PostAsync(body);
        if (response?.status is "updated" or "kept_newer")
        {
            LocalStore.SetString(TimestampKey, response.updated_at ?? timestamp.ToString("O"));
            if (response.status == "kept_newer" && !string.IsNullOrWhiteSpace(response.payload)) Apply(response.payload);
        }
    }

    private static void Apply(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw); var root = document.RootElement;
            SetString(root, "theme_accent_hex", "theme-accent"); SetBool(root, "manaba_smartphone", "manaba-smartphone");
            SetBool(root, "portal_smartphone", "portal-smartphone"); SetString(root, "term_preference", "term-preference");
            SetRaw(root, "service_tabs", "service-tabs"); SetRaw(root, "course_colors", "course-colors");
            SetRaw(root, "course_names", "course-names"); SetRaw(root, "course_notes", "course-notes");
        }
        catch { }
    }
    private static void SetString(JsonElement root, string property, string key) { if (root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String) LocalStore.SetString(key, value.GetString() ?? ""); }
    private static void SetBool(JsonElement root, string property, string key) { if (root.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False) LocalStore.SetString(key, value.GetBoolean() ? "true" : "false"); }
    private static void SetRaw(JsonElement root, string property, string key) { if (root.TryGetProperty(property, out var value)) LocalStore.SetString(key, value.GetRawText()); }
    private static DateTimeOffset Parse(string? raw) => DateTimeOffset.TryParse(raw, out var value) ? value : DateTimeOffset.MinValue;
    private static async Task<Response?> PostAsync(Dictionary<string, string> body)
    {
        try { using var response = await Client.PostAsJsonAsync(ApiUrl, body); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Response>() : null; }
        catch { return null; }
    }
    private sealed class Response { public bool success { get; set; } public string? status { get; set; } public string? payload { get; set; } public string? updated_at { get; set; } }
}
