using System.Text.Json;
using CITHub.Windows.Models;
using Windows.Storage;
using Windows.Security.Credentials;

namespace CITHub.Windows.Services;

public static class LocalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string Folder => ApplicationData.Current.LocalFolder.Path;

    public static async Task<List<PersonalTodo>> LoadTodosAsync()
    {
        var path = Path.Combine(Folder, "personal-todos.json");
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<PersonalTodo>>(stream, JsonOptions) ?? [];
    }

    public static async Task SaveTodosAsync(IEnumerable<PersonalTodo> todos)
    {
        var path = Path.Combine(Folder, "personal-todos.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, todos, JsonOptions);
    }

    public static string GetString(string key, string defaultValue = "") =>
        ApplicationData.Current.LocalSettings.Values[key] as string ?? defaultValue;

    public static void SetString(string key, string value) =>
        ApplicationData.Current.LocalSettings.Values[key] = value;
}

public static class WindowsCredentialStore
{
    private const string Resource = "CITHub.Windows";

    public static void Save(string key, string value)
    {
        var vault = new PasswordVault();
        try { vault.Remove(vault.Retrieve(Resource, key)); } catch { }
        vault.Add(new PasswordCredential(Resource, key, value));
    }

    public static string? Load(string key)
    {
        try { var credential = new PasswordVault().Retrieve(Resource, key); credential.RetrievePassword(); return credential.Password; }
        catch { return null; }
    }
}
