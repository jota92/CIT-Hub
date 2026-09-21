using System.Text.Json;
using CITHub.Windows.Models;
using Windows.Storage;

namespace CITHub.Windows.Services;

public static class TimetableStore
{
    private const string FileName = "timetable-courses.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string Path => System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName);

    public static async Task<List<TimetableCourse>> LoadAsync()
    {
        if (!File.Exists(Path)) return [];
        await using var stream = File.OpenRead(Path);
        return await JsonSerializer.DeserializeAsync<List<TimetableCourse>>(stream, JsonOptions) ?? [];
    }

    public static async Task SaveAsync(IEnumerable<TimetableCourse> courses)
    {
        await using var stream = File.Create(Path);
        await JsonSerializer.SerializeAsync(stream, courses.OrderBy(course => course.Day).ThenBy(course => course.Period), JsonOptions);
        LocalStore.SetString("timetable-updated-at", DateTimeOffset.UtcNow.ToString("O"));
    }

    public static string CourseId(string day, string period, string title, string teacher, string classroom) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{day}|{period}|{title}|{teacher}|{classroom}"))).ToLowerInvariant()[..24];
}
