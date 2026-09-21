using System.Text.Json;
using CITHub.Windows.Models;
using Windows.Storage;

namespace CITHub.Windows.Services;

public static class AssignmentStore
{
    private static string FilePath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "manaba-assignments.json");
    public static async Task<List<ManabaAssignment>> LoadAsync()
    {
        if (!File.Exists(FilePath)) return [];
        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<List<ManabaAssignment>>(stream) ?? [];
    }
    public static async Task SaveAsync(IEnumerable<ManabaAssignment> assignments)
    {
        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, assignments);
        LocalStore.SetString("assignments-updated-at", DateTimeOffset.UtcNow.ToString("O"));
    }
}
