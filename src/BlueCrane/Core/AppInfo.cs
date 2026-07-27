using System.IO;

namespace BlueCrane.Core;

/// <summary>Product identity and the one place on disk the browser writes to.</summary>
public static class AppInfo
{
    public const string Name = "Blue Crane Browser";

    /// <summary>%LOCALAPPDATA%\BlueCrane — profile, cache and logs all live here.</summary>
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlueCrane");
}
