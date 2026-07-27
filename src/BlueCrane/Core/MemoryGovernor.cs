using System.Diagnostics;
using System.Windows.Threading;

namespace BlueCrane.Core;

/// <summary>
/// Decides when a background tab stops being worth its memory.
///
/// Two thresholds, both measured from the moment a tab left the foreground:
/// after <see cref="SuspendAfter"/> the renderer is frozen but kept, after
/// <see cref="DiscardAfter"/> it is thrown away. A third rule overrides both — if total
/// footprint crosses <see cref="PressureThresholdMb"/>, the least-recently-used dormant
/// tabs are discarded early regardless of how long they have been idle.
/// </summary>
public sealed class MemoryGovernor
{
    private readonly DispatcherTimer _timer;
    private readonly Func<IReadOnlyList<BrowserTab>> _tabs;
    private readonly Func<BrowserTab?> _active;
    private bool _running;

    public TimeSpan SuspendAfter { get; set; } = TimeSpan.FromSeconds(90);
    public TimeSpan DiscardAfter { get; set; } = TimeSpan.FromMinutes(10);
    public int PressureThresholdMb { get; set; } = 1200;

    /// <summary>Total working set of the shell plus every WebView2 process it owns, in MB.</summary>
    public int FootprintMb { get; private set; }

    public event Action? FootprintChanged;

    public MemoryGovernor(Func<IReadOnlyList<BrowserTab>> tabs, Func<BrowserTab?> active)
    {
        _tabs = tabs;
        _active = active;

        // 15s is frequent enough that thresholds feel exact and rare enough to be free.
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(15),
        };
        _timer.Tick += async (_, _) => await TickAsync();
    }

    public void Start()
    {
        // Publish a real number straight away; waiting for the first tick would leave
        // the readout showing 0 MB for the first quarter minute.
        UpdateFootprint();
        _timer.Start();
    }

    private async Task TickAsync()
    {
        if (_running) return;   // a slow suspend must not overlap the next tick
        _running = true;

        try
        {
            var now = DateTime.UtcNow;
            var active = _active();

            foreach (var tab in _tabs())
            {
                if (ReferenceEquals(tab, active)) continue;

                var idle = now - tab.LastActiveUtc;

                switch (tab.State)
                {
                    case TabState.Live when idle >= SuspendAfter:
                        await tab.SuspendAsync();
                        break;

                    case TabState.Suspended when idle >= DiscardAfter:
                        tab.Discard();
                        break;
                }
            }

            UpdateFootprint();
            RelievePressure(active);
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>Discard oldest-first until the footprint is back under the ceiling.</summary>
    private void RelievePressure(BrowserTab? active)
    {
        if (FootprintMb <= PressureThresholdMb) return;

        var candidates = _tabs()
            .Where(t => !ReferenceEquals(t, active) && t.State != TabState.Asleep)
            .OrderBy(t => t.LastActiveUtc)
            .ToList();

        foreach (var tab in candidates)
        {
            tab.Discard();
            UpdateFootprint();
            if (FootprintMb <= PressureThresholdMb) break;
        }
    }

    private void UpdateFootprint()
    {
        long bytes = 0;

        using (var self = Process.GetCurrentProcess())
        {
            bytes += self.WorkingSet64;
        }

        // Renderers, GPU and network live in separate processes; the shell's own working
        // set says almost nothing without them.
        if (BrowserEnvironment.Current is { } env)
        {
            foreach (var info in env.GetProcessInfos())
            {
                try
                {
                    using var proc = Process.GetProcessById(info.ProcessId);
                    bytes += proc.WorkingSet64;
                }
                catch (ArgumentException)
                {
                    // Process exited between enumeration and lookup.
                }
            }
        }

        var mb = (int)(bytes / (1024 * 1024));
        if (mb == FootprintMb) return;

        FootprintMb = mb;
        FootprintChanged?.Invoke();
    }
}
