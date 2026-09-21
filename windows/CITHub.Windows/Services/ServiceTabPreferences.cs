using System.Text.Json;

namespace CITHub.Windows.Services;

public sealed record ServiceTabPreference(string id, string kind, string title, string? urlString, bool isVisible);

public static class ServiceTabPreferences
{
    private const string Key = "service-tabs";
    private static readonly string[] BuiltinKinds = ["manaba", "portal", "cafeteriaMenu", "busSchedule"];

    public static IReadOnlyList<ServiceTabPreference> Load()
    {
        List<ServiceTabPreference>? saved = null;
        try
        {
            saved = JsonSerializer.Deserialize<List<ServiceTabPreference>>(LocalStore.GetString(Key, "[]"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { }

        saved ??= [];
        var result = new List<ServiceTabPreference>();
        foreach (var kind in BuiltinKinds)
        {
            var item = saved.LastOrDefault(value => string.Equals(value.kind, kind, StringComparison.OrdinalIgnoreCase));
            result.Add(item ?? new ServiceTabPreference($"builtin.{kind}", kind, DefaultTitle(kind), null, true));
        }

        result.AddRange(saved.Where(value => string.Equals(value.kind, "custom", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(value.title)
            && Uri.TryCreate(value.urlString, UriKind.Absolute, out _)));
        return result;
    }

    public static void Save(IEnumerable<ServiceTabPreference> items)
    {
        LocalStore.SetString(Key, JsonSerializer.Serialize(items));
        _ = UserPreferencesSyncService.UploadAsync();
    }

    public static string DefaultTitle(string kind) => kind switch
    {
        "manaba" => "manaba",
        "portal" => "ポータル",
        "cafeteriaMenu" => "食堂メニュー",
        "busSchedule" => "バスダイヤ",
        _ => kind
    };
}
