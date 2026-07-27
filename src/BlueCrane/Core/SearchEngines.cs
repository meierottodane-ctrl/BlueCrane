namespace BlueCrane.Core;

/// <param name="Id">Stable key written to settings.json.</param>
/// <param name="Name">Label shown on the setup screen.</param>
/// <param name="Template">Query URL with {0} where the escaped terms go.</param>
public sealed record SearchEngine(string Id, string Name, string Template);

/// <summary>The engines offered on first run. Adding one is a single entry here.</summary>
public static class SearchEngines
{
    public static readonly SearchEngine WebCrawler =
        new("webcrawler", "WebCrawler", "https://www.webcrawler.com/serp?q={0}");

    public static readonly SearchEngine Google =
        new("google", "Google", "https://www.google.com/search?q={0}");

    public static readonly SearchEngine Bing =
        new("bing", "Bing", "https://www.bing.com/search?q={0}");

    public static readonly SearchEngine DuckDuckGo =
        new("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q={0}");

    public static SearchEngine Default => WebCrawler;

    public static IReadOnlyList<SearchEngine> All { get; } = [WebCrawler, Google, Bing, DuckDuckGo];

    /// <summary>Looks up an engine by id, falling back to the default for unknown values.</summary>
    public static SearchEngine Resolve(string? id)
        => All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
