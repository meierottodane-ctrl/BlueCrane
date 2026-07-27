using System.Text.RegularExpressions;

namespace BlueCrane.Core;

/// <summary>Resolves what the user typed into a navigable URI: address, or web search.</summary>
public static partial class Omnibox
{
    // host[:port] with at least one dot and a plausible TLD, or bare localhost.
    [GeneratedRegex(@"^(localhost|(\d{1,3}\.){3}\d{1,3}|([\w-]+\.)+[a-z]{2,63})(:\d{1,5})?(/.*)?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex HostLike();

    public static string Resolve(string input)
    {
        var text = input.Trim();
        if (text.Length == 0) return StartPage.Url;

        // An explicit scheme is always taken at face value.
        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute) &&
            absolute.Scheme is "http" or "https" or "file" or "about" or "edge" or "data")
        {
            return absolute.ToString();
        }

        // A single token that parses as a host is an address; anything with a space is a query.
        if (!text.Contains(' ') && HostLike().IsMatch(text))
        {
            return "https://" + text;
        }

        // Anything that isn't an address goes to whichever engine the user picked.
        return string.Format(SettingsStore.Engine.Template, Uri.EscapeDataString(text));
    }

    /// <summary>The short form shown in the address bar when the tab is not focused.</summary>
    public static string Display(string url)
    {
        // The built-in start page has no address worth showing.
        if (StartPage.IsInternal(url)) return string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        if (uri.Scheme == "about") return string.Empty;

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;

        var path = uri.PathAndQuery == "/" ? string.Empty : uri.PathAndQuery;
        return host + path;
    }
}
