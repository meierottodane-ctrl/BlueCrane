using System.IO;
using Microsoft.Web.WebView2.Core;

namespace BlueCrane.Core;

/// <summary>
/// The one <see cref="CoreWebView2Environment"/> every tab is built from.
///
/// This is the foundation of the whole memory story: WebView2 instances sharing an
/// environment also share a single browser process, one GPU process, and one network
/// service. Each additional tab then costs a renderer, not a full browser stack.
/// Creating a second environment would double that fixed overhead.
/// </summary>
public static class BrowserEnvironment
{
    private static Task<CoreWebView2Environment>? _pending;

    private static readonly string[] BrowserArguments =
    [
        // Same-site tabs share one renderer instead of getting one each. Cross-site
        // isolation boundaries are unchanged — this only collapses duplicates, and it
        // is the single largest win when many tabs are open on a few domains.
        "--process-per-site",

        // Background services that cost memory and wakeups but add nothing to a
        // minimal browsing shell.
        "--disable-background-networking",
        "--disable-features=Translate,MediaRouter,OptimizationHints,AutofillServerCommunication,InterestFeedContentSuggestions",

        // Let Chromium release compositing resources for windows it can prove are
        // covered. Pairs with our own suspend pass.
        "--enable-features=CalculateNativeWinOcclusion",
    ];

    public static string UserDataFolder { get; } = Path.Combine(AppInfo.DataFolder, "WebView2");

    /// <summary>The environment if it has finished initialising, else null. Never blocks.</summary>
    public static CoreWebView2Environment? Current
        => _pending is { IsCompletedSuccessfully: true } task ? task.Result : null;

    public static Task<CoreWebView2Environment> GetAsync()
    {
        // Cache the task, not the result: concurrent tab creations during startup all
        // await the same in-flight initialisation.
        return _pending ??= CreateAsync();
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(UserDataFolder);

        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = string.Join(' ', BrowserArguments),
            AreBrowserExtensionsEnabled = false,
        };

        return await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: UserDataFolder,
            options: options);
    }
}
