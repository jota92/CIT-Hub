using System.Text.Json;
using Microsoft.UI.Dispatching;
using Windows.UI.Notifications;

namespace CITHub.Windows.Services;

public static class ForegroundNoticePoller
{
    private const string SeenKey = "windows-seen-admin-notices";
    private static DispatcherQueueTimer? _timer;
    private static bool _initialized;

    public static void Start(DispatcherQueue dispatcherQueue)
    {
        _timer ??= dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMinutes(5);
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();
        _ = PollAsync();
    }

    private static async Task PollAsync()
    {
        if (!WindowsDeviceSessionService.IsLinked) return;
        var notices = await AdminNotificationService.LoadAsync();
        var seen = LoadSeen();
        if (!_initialized && seen.Count == 0)
        {
            SaveSeen(notices.Select(notice => notice.id));
            _initialized = true;
            return;
        }
        _initialized = true;
        var newNotices = notices.Where(notice => !seen.Contains(notice.id)).ToList();
        foreach (var notice in newNotices) Show(notice);
        if (newNotices.Count > 0) SaveSeen(seen.Concat(newNotices.Select(notice => notice.id)));
    }

    private static HashSet<string> LoadSeen()
    {
        try { return JsonSerializer.Deserialize<HashSet<string>>(LocalStore.GetString(SeenKey, "[]")) ?? []; }
        catch { return []; }
    }
    private static void SaveSeen(IEnumerable<string> ids) => LocalStore.SetString(SeenKey, JsonSerializer.Serialize(ids.Where(id => !string.IsNullOrWhiteSpace(id)).Take(500).ToHashSet()));
    private static void Show(AdminNotice notice)
    {
        try
        {
            var document = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var text = document.GetElementsByTagName("text");
            text[0].AppendChild(document.CreateTextNode(notice.title));
            text[1].AppendChild(document.CreateTextNode(notice.message));
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(document));
        }
        catch { }
    }
}
