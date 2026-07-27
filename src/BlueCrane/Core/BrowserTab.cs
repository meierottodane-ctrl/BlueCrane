using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace BlueCrane.Core;

public enum TabState
{
    /// <summary>No renderer exists. The tab is a URL, a title and a favicon — a few KB.</summary>
    Asleep,

    /// <summary>Renderer live and attached.</summary>
    Live,

    /// <summary>Renderer exists but is frozen: no timers, no script, trimmed working set.</summary>
    Suspended,
}

/// <summary>
/// One tab. Owns its <see cref="WebView2"/> for exactly as long as it needs to.
///
/// The lifecycle is the point of this class. A tab moves Asleep → Live on activation,
/// Live → Suspended when it has been in the background a short while, and
/// Suspended → Asleep when it has been there a long while. Only the middle state costs
/// real memory, and only for tabs the user has actually looked at recently.
/// </summary>
public sealed class BrowserTab : Observable, IDisposable
{
    private readonly Panel _host;
    private WebView2? _view;

    private string _url;
    private string _title = "New tab";
    private TabState _state = TabState.Asleep;
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;
    private ImageSource? _favicon;

    public BrowserTab(Panel host, string url)
    {
        _host = host;
        _url = url;

        var label = Omnibox.Display(url);
        if (label.Length > 0) _title = label;
    }

    /// <summary>Raised when the page asks for a new window (target=_blank, window.open).</summary>
    public event Action<string>? NewTabRequested;

    public string Url
    {
        get => _url;
        private set { if (Set(ref _url, value)) Raise(nameof(DisplayUrl)); }
    }

    public string DisplayUrl => Omnibox.Display(_url);
    public string Title { get => _title; private set => Set(ref _title, value); }
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public bool CanGoBack { get => _canGoBack; private set => Set(ref _canGoBack, value); }
    public bool CanGoForward { get => _canGoForward; private set => Set(ref _canGoForward, value); }
    public ImageSource? Favicon { get => _favicon; private set => Set(ref _favicon, value); }

    public TabState State
    {
        get => _state;
        private set { if (Set(ref _state, value)) Raise(nameof(IsDormant)); }
    }

    /// <summary>True when the tab is holding no live renderer — surfaced in the tab strip.</summary>
    public bool IsDormant => _state != TabState.Live;

    private bool _isActive;

    /// <summary>Selection state, owned by the window that hosts the strip.</summary>
    public bool IsActive { get => _isActive; internal set => Set(ref _isActive, value); }

    /// <summary>When this tab was last the foreground tab. Drives the memory governor.</summary>
    public DateTime LastActiveUtc { get; private set; } = DateTime.UtcNow;

    // ─────────────────────────── Activation ───────────────────────────

    public async Task ActivateAsync()
    {
        LastActiveUtc = DateTime.UtcNow;

        switch (State)
        {
            case TabState.Live:
                break;

            case TabState.Suspended:
                // Cheap path: the renderer is still there, page state intact.
                _view!.CoreWebView2.Resume();
                _view.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                State = TabState.Live;
                break;

            case TabState.Asleep:
                await CreateViewAsync();
                break;
        }

        if (_view is not null) _view.Visibility = Visibility.Visible;
    }

    public void Deactivate()
    {
        LastActiveUtc = DateTime.UtcNow;
        if (_view is not null) _view.Visibility = Visibility.Collapsed;
    }

    private async Task CreateViewAsync()
    {
        var env = await BrowserEnvironment.GetAsync();

        var view = new WebView2
        {
            Visibility = Visibility.Collapsed,
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x0F, 0x24, 0x34),
        };

        _view = view;
        _host.Children.Add(view);

        await view.EnsureCoreWebView2Async(env);
        WireCore(view.CoreWebView2);

        State = TabState.Live;
        Navigate(_url);
    }

    private void WireCore(CoreWebView2 core)
    {
        // Serve the built-in new tab page from disk under its own host name.
        core.SetVirtualHostNameToFolderMapping(
            StartPage.Host, StartPage.AssetsFolder, CoreWebView2HostResourceAccessKind.Allow);

        var s = core.Settings;
        s.AreDevToolsEnabled = true;
        s.AreDefaultContextMenusEnabled = true;
        s.IsSwipeNavigationEnabled = true;
        s.IsStatusBarEnabled = true;
        // No password manager or autofill store: this shell keeps no credential state.
        s.IsPasswordAutosaveEnabled = false;
        s.IsGeneralAutofillEnabled = false;

        core.DocumentTitleChanged += (_, _) =>
            Title = string.IsNullOrWhiteSpace(core.DocumentTitle) ? Omnibox.Display(_url) : core.DocumentTitle;

        core.SourceChanged += (_, _) => Url = core.Source;

        core.HistoryChanged += (_, _) =>
        {
            CanGoBack = core.CanGoBack;
            CanGoForward = core.CanGoForward;
        };

        core.NavigationStarting += (_, _) => IsLoading = true;

        core.NavigationCompleted += (_, _) =>
        {
            IsLoading = false;
            Url = core.Source;
        };

        core.FaviconChanged += async (_, _) => await LoadFaviconAsync(core);

        // Popups become tabs. A browser this small has no business opening chrome-less
        // windows it cannot manage.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            NewTabRequested?.Invoke(e.Uri);
        };

        // The built-in pages hand their intent back to us rather than acting on their
        // own, so search engines and settings stay a host-side concern.
        core.WebMessageReceived += (_, e) =>
        {
            try
            {
                using var message = JsonDocument.Parse(e.WebMessageAsJson);
                var root = message.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                switch (type)
                {
                    case "navigate" when Value(root) is { Length: > 0 } query:
                        Navigate(query);
                        break;

                    case "setup" when Value(root) is { Length: > 0 } engineId:
                        SettingsStore.Current.SearchEngine = SearchEngines.Resolve(engineId).Id;
                        SettingsStore.Current.SetupComplete = true;
                        SettingsStore.Save();
                        Navigate(StartPage.Url);
                        break;
                }
            }
            catch (JsonException)
            {
                // Only our own pages post messages; a malformed one is not actionable.
            }
        };

        static string? Value(JsonElement root)
            => root.TryGetProperty("value", out var v) ? v.GetString() : null;
    }

    private async Task LoadFaviconAsync(CoreWebView2 core)
    {
        try
        {
            await using var stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            if (stream is null) { Favicon = null; return; }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;   // decode now, release the stream
            image.DecodePixelWidth = 16;                    // never hold a 256px icon for a 16px slot
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            Favicon = image;
        }
        catch
        {
            // A missing or malformed favicon is not worth surfacing.
            Favicon = null;
        }
    }

    // ─────────────────────────── Memory transitions ───────────────────────────

    /// <summary>
    /// Freeze the renderer. Chromium stops all timers and script and trims the working set,
    /// but the page's DOM and scroll position survive, so resume is instant and offline-safe.
    /// </summary>
    public async Task<bool> SuspendAsync()
    {
        if (State != TabState.Live || _view?.CoreWebView2 is not { } core) return false;

        // WebView2 refuses to suspend a visible surface.
        _view.Visibility = Visibility.Collapsed;
        core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;

        try
        {
            // Fails while media plays or a download is running — correct behaviour, retry later.
            if (!await core.TrySuspendAsync()) return false;
        }
        catch (Exception)
        {
            return false;
        }

        State = TabState.Suspended;
        return true;
    }

    /// <summary>
    /// Tear the renderer down entirely, keeping only URL, title and favicon. This is what
    /// makes a large tab count affordable; the cost is a reload when the user returns.
    /// </summary>
    public void Discard()
    {
        if (State == TabState.Asleep) return;

        DestroyView();
        State = TabState.Asleep;
    }

    private void DestroyView()
    {
        if (_view is null) return;

        _host.Children.Remove(_view);
        _view.Dispose();
        _view = null;
    }

    // ─────────────────────────── Navigation ───────────────────────────

    public void Navigate(string input)
    {
        var target = Omnibox.Resolve(input);
        Url = target;

        if (_view?.CoreWebView2 is { } core)
        {
            core.Navigate(target);
        }
        else
        {
            // Asleep: the URL is stored and loads on next activation.
            Title = Omnibox.Display(target);
        }
    }

    public void GoBack() { if (_view?.CoreWebView2 is { CanGoBack: true } c) c.GoBack(); }
    public void GoForward() { if (_view?.CoreWebView2 is { CanGoForward: true } c) c.GoForward(); }
    public void Reload() { if (_view?.CoreWebView2 is { } c) c.Reload(); }
    public void Stop() { if (_view?.CoreWebView2 is { } c) c.Stop(); }

    public void Focus() => _view?.Focus();

    public void Dispose() => DestroyView();
}
