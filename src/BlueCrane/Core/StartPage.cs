using System.IO;

namespace BlueCrane.Core;

/// <summary>
/// The built-in new tab page.
///
/// It is served from disk through a WebView2 virtual host rather than injected with
/// NavigateToString, which costs nothing extra and buys three things: the page has a
/// real URL so back/forward and reload behave normally, its images load as ordinary
/// requests instead of megabytes of inlined base64, and it never touches the network.
/// </summary>
public static class StartPage
{
    /// <summary>Virtual host the assets folder is mapped onto. Not a real domain.</summary>
    public const string Host = "newtab.crane";

    public const string Url = $"https://{Host}/index.html";

    /// <summary>First-run configuration, also reachable later from the start page.</summary>
    public const string SetupUrl = $"https://{Host}/setup.html";

    /// <summary>Where the first tab of a session should open.</summary>
    public static string Landing => SettingsStore.Current.SetupComplete ? Url : SetupUrl;

    public static string AssetsFolder { get; } = Path.Combine(AppContext.BaseDirectory, "Assets");

    /// <summary>True for any URL served by the built-in page, which the omnibox hides.</summary>
    public static bool IsInternal(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
           uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase);
}
