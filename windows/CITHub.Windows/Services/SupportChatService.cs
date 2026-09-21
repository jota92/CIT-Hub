using System.Net.Http.Json;

namespace CITHub.Windows.Services;

public static class SupportChatService
{
    private const string ApiUrl = "https://cithub-api.johta0902.workers.dev?endpoint=support-chat";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<IReadOnlyList<SupportChatMessage>> LoadAsync()
    {
        var body = WindowsDeviceSessionService.AuthorizedBody();
        if (body is null) return [];
        body["operation"] = "list";
        var response = await PostAsync(body);
        return response?.messages ?? [];
    }

    public static async Task<SupportChatMessage?> SendAsync(string message)
    {
        var body = WindowsDeviceSessionService.AuthorizedBody();
        if (body is null || string.IsNullOrWhiteSpace(message)) return null;
        body["operation"] = "send";
        body["message"] = message.Trim();
        return (await PostAsync(body))?.message;
    }

    private static async Task<SupportChatResponse?> PostAsync(Dictionary<string, string> body)
    {
        try
        {
            using var response = await Client.PostAsJsonAsync(ApiUrl, body);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SupportChatResponse>() : null;
        }
        catch { return null; }
    }
}

public sealed class SupportChatMessage
{
    public string id { get; set; } = "";
    public string sender { get; set; } = "";
    public string body { get; set; } = "";
    public string created_at { get; set; } = "";
    public bool is_read { get; set; }
    public string Display => $"{(sender == "admin" ? "運営" : "あなた")}  {body}";
    public string SenderDisplay => sender == "admin" ? "運営" : "あなた";
}

public sealed class SupportChatResponse { public bool success { get; set; } public List<SupportChatMessage>? messages { get; set; } public SupportChatMessage? message { get; set; } }
