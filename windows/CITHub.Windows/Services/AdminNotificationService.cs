using System.Net.Http.Json;

namespace CITHub.Windows.Services;

public static class AdminNotificationService
{
    private const string ApiUrl = "https://cithub-api.johta0902.workers.dev?endpoint=admin-notifications";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<IReadOnlyList<AdminNotice>> LoadAsync()
    {
        var body = WindowsDeviceSessionService.AuthorizedBody();
        if (body is null) return [];
        try
        {
            using var response = await Client.PostAsJsonAsync(ApiUrl, body);
            var payload = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Response>() : null;
            return payload?.notifications ?? [];
        }
        catch { return []; }
    }
}

public sealed class AdminNotice
{
    public string id { get; set; } = "";
    public string title { get; set; } = "お知らせ";
    public string message { get; set; } = "";
    public string sent_at { get; set; } = "";
    public string url { get; set; } = "";
    public string Display => $"{title}\n{message}";
}

public sealed class Response { public bool success { get; set; } public List<AdminNotice>? notifications { get; set; } }
