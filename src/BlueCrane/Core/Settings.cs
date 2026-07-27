using System.IO;
using System.Text.Json;

namespace BlueCrane.Core;

/// <summary>User choices that outlive a session. Deliberately tiny.</summary>
public sealed class Settings
{
    public string SearchEngine { get; set; } = SearchEngines.Default.Id;

    /// <summary>False until the setup screen has been completed once.</summary>
    public bool SetupComplete { get; set; }
}

/// <summary>
/// Loads and saves <see cref="Settings"/> as JSON next to the profile.
///
/// Settings are read once at startup and held in memory; a browser that re-reads its
/// configuration on every keystroke is a browser that touches the disk for no reason.
/// </summary>
public static class SettingsStore
{
    private static readonly string Path =
        System.IO.Path.Combine(AppInfo.DataFolder, "settings.json");

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public static Settings Current { get; } = Load();

    /// <summary>The engine every non-address omnibox entry is sent to.</summary>
    public static SearchEngine Engine => SearchEngines.Resolve(Current.SearchEngine);

    private static Settings Load()
    {
        try
        {
            return File.Exists(Path)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? new Settings()
                : new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable file must not stop the browser from starting.
            return new Settings();
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataFolder);
            File.WriteAllText(Path, JsonSerializer.Serialize(Current, Format));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing a preference is not worth taking the window down for.
        }
    }
}
