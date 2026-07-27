using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BlueCrane.Core;
using BlueCrane.Shell;

namespace BlueCrane;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const string AppName = AppInfo.Name;
    private static readonly string HomePage = StartPage.Url;

    private readonly MemoryGovernor _governor;
    private BrowserTab? _active;

    public ObservableCollection<BrowserTab> Tabs { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        WindowFrame.Apply(this, RootGrid);
        StateChanged += (_, _) => SyncMaximizeGlyph();

        _governor = new MemoryGovernor(() => Tabs, () => _active);
        _governor.FootprintChanged += SyncFootprint;

        Loaded += async (_, _) =>
        {
            // Only the first tab of a session can land on setup; every later tab is a
            // normal new tab.
            await OpenTabAsync(StartPage.Landing);
            _governor.Start();
        };
    }

    // ─────────────────────────── Tabs ───────────────────────────

    private BrowserTab CreateTab(string url)
    {
        var tab = new BrowserTab(ViewHost, Omnibox.Resolve(url));
        tab.NewTabRequested += target => CreateTab(target);   // background tab, stays asleep
        Tabs.Add(tab);
        SyncFootprint();
        return tab;
    }

    private async Task OpenTabAsync(string url)
    {
        await ActivateAsync(CreateTab(url));
    }

    private async Task ActivateAsync(BrowserTab tab)
    {
        if (ReferenceEquals(_active, tab))
        {
            tab.Focus();
            return;
        }

        if (_active is { } previous)
        {
            previous.PropertyChanged -= ActiveTabPropertyChanged;
            previous.IsActive = false;
            previous.Deactivate();
        }

        _active = tab;
        tab.IsActive = true;
        tab.PropertyChanged += ActiveTabPropertyChanged;

        await tab.ActivateAsync();

        SyncChrome();
        tab.Focus();
    }

    private async Task CloseTabAsync(BrowserTab tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

        var wasActive = ReferenceEquals(_active, tab);
        if (wasActive)
        {
            tab.PropertyChanged -= ActiveTabPropertyChanged;
            _active = null;
        }

        Tabs.Remove(tab);
        tab.Dispose();

        if (Tabs.Count == 0)
        {
            Close();
            return;
        }

        if (wasActive)
        {
            await ActivateAsync(Tabs[Math.Min(index, Tabs.Count - 1)]);
        }

        SyncFootprint();
    }

    private void ActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BrowserTab.CanGoBack):
            case nameof(BrowserTab.CanGoForward):
                SyncNavButtons();
                break;

            case nameof(BrowserTab.IsLoading):
                SyncReloadButton();
                break;

            case nameof(BrowserTab.Url):
                SyncAddressBar();
                break;

            case nameof(BrowserTab.Title):
                Title = _active is null ? AppName : $"{_active.Title} — {AppName}";
                break;

            case nameof(BrowserTab.State):
                SyncFootprint();
                break;
        }
    }

    // ─────────────────────────── Chrome sync ───────────────────────────

    private void SyncChrome()
    {
        SyncNavButtons();
        SyncReloadButton();
        SyncAddressBar();
        SyncFootprint();
        Title = _active is null ? AppName : $"{_active.Title} — {AppName}";
    }

    private void SyncNavButtons()
    {
        BackButton.IsEnabled = _active?.CanGoBack ?? false;
        ForwardButton.IsEnabled = _active?.CanGoForward ?? false;
    }

    private void SyncReloadButton()
    {
        var loading = _active?.IsLoading ?? false;
        ReloadGlyph.Data = (Geometry)FindResource(loading ? "Icon.Close" : "Icon.Reload");
        ReloadButton.ToolTip = loading ? "Stop  (Esc)" : "Reload  (Ctrl+R)";
    }

    private void SyncAddressBar()
    {
        // Never rewrite the field mid-edit; the user's caret outranks a navigation event.
        if (AddressBar.IsKeyboardFocused) return;
        AddressBar.Text = _active?.DisplayUrl ?? string.Empty;
    }

    private void SyncMaximizeGlyph()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaxGlyph.Data = (Geometry)FindResource(maximized ? "Icon.Restore" : "Icon.Maximize");
        MaxButton.ToolTip = maximized ? "Restore" : "Maximise";
    }

    private void SyncFootprint()
    {
        Raise(nameof(FootprintLabel));
        Raise(nameof(FootprintDetail));
    }

    public string FootprintLabel => $"{_governor.FootprintMb} MB";

    public string FootprintDetail
    {
        get
        {
            var live = Tabs.Count(t => t.State == TabState.Live);
            var suspended = Tabs.Count(t => t.State == TabState.Suspended);
            var asleep = Tabs.Count(t => t.State == TabState.Asleep);

            return $"{_governor.FootprintMb} MB across every browser process\n" +
                   $"{Tabs.Count} tabs — {live} live, {suspended} suspended, {asleep} unloaded\n\n" +
                   $"Tabs freeze after {_governor.SuspendAfter.TotalSeconds:0}s in the background " +
                   $"and unload after {_governor.DiscardAfter.TotalMinutes:0} min.";
        }
    }

    // ─────────────────────────── Input ───────────────────────────

    protected override async void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        switch (e.Key)
        {
            case Key.T when ctrl:
                e.Handled = true;
                // No FocusAddressBar here — the start page puts the caret in its own
                // search field, and stealing it back would fight the user.
                await OpenTabAsync(HomePage);
                break;

            case Key.W when ctrl:
                e.Handled = true;
                if (_active is { } closing) await CloseTabAsync(closing);
                break;

            case Key.L when ctrl:
            case Key.D when alt:
            case Key.F6:
                e.Handled = true;
                FocusAddressBar();
                break;

            case Key.R when ctrl:
            case Key.F5:
                e.Handled = true;
                _active?.Reload();
                break;

            case Key.Tab when ctrl:
                e.Handled = true;
                await CycleTabAsync(shift ? -1 : 1);
                break;

            case Key.Left when alt:
                e.Handled = true;
                _active?.GoBack();
                break;

            case Key.Right when alt:
                e.Handled = true;
                _active?.GoForward();
                break;

            case >= Key.D1 and <= Key.D9 when ctrl:
                e.Handled = true;
                var slot = e.Key - Key.D1;
                var target = slot == 8 ? Tabs.Count - 1 : slot;   // Ctrl+9 is "last tab"
                if (target >= 0 && target < Tabs.Count) await ActivateAsync(Tabs[target]);
                break;
        }
    }

    private async Task CycleTabAsync(int delta)
    {
        if (_active is null || Tabs.Count < 2) return;

        var index = (Tabs.IndexOf(_active) + delta + Tabs.Count) % Tabs.Count;
        await ActivateAsync(Tabs[index]);
    }

    private void FocusAddressBar()
    {
        AddressBar.Focus();
        AddressBar.SelectAll();
    }

    // ─────────────────────────── Handlers ───────────────────────────

    private async void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is BrowserTab tab) await ActivateAsync(tab);
    }

    private async void Tab_MiddleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        if (((FrameworkElement)sender).DataContext is BrowserTab tab) await CloseTabAsync(tab);
    }

    private async void TabClose_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;   // don't let the click fall through and select the tab
        if (((FrameworkElement)sender).DataContext is BrowserTab tab) await CloseTabAsync(tab);
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await OpenTabAsync(HomePage);

    private void Back_Click(object sender, RoutedEventArgs e) => _active?.GoBack();
    private void Forward_Click(object sender, RoutedEventArgs e) => _active?.GoForward();

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_active is not { } tab) return;
        if (tab.IsLoading) tab.Stop(); else tab.Reload();
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                _active?.Navigate(AddressBar.Text);
                _active?.Focus();
                break;

            case Key.Escape:
                e.Handled = true;
                AddressBar.Text = _active?.DisplayUrl ?? string.Empty;
                _active?.Focus();
                break;
        }
    }

    private void AddressBar_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Editing works on the real URL rather than the shortened display form — except
        // on the built-in pages, whose internal address is noise. There the bar opens
        // empty and ready to be typed into.
        var url = _active?.Url;
        AddressBar.Text = url is null || StartPage.IsInternal(url) ? string.Empty : url;
        AddressBar.SelectAll();
    }

    private void AddressBar_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => SyncAddressBar();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        foreach (var tab in Tabs) tab.Dispose();
        Tabs.Clear();
        base.OnClosed(e);
    }

    // ─────────────────────────── INotifyPropertyChanged ───────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
