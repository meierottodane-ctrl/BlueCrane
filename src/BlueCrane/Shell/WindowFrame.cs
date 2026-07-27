using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace BlueCrane.Shell;

/// <summary>
/// Owns the inset between the window and its content, which has to solve two problems
/// at once.
///
/// Restored, the content is held back by <see cref="Gutter"/> so a strip of plain
/// window belongs to WPF on every edge. Without it there is nothing to grab: WebView2
/// hosts a child HWND that swallows mouse input before the window sees a hit-test, so
/// a page reaching the window edge makes that edge un-resizable. Insetting the whole
/// root — not just the page — also keeps the tab strip, the address band and the page
/// on a single shared edge.
///
/// Maximised, a WindowChrome window is sized to the work area plus its resize border,
/// and since the chrome extends the client area to the full window rect, that border
/// width of content falls off every side. Answering WM_GETMINMAXINFO is the usual fix,
/// but WPF's own WindowChromeWorker handles that message first and marks it handled, so
/// a later hook never runs. Measuring the overhang and insetting by exactly that much
/// works regardless of who answered.
/// </summary>
public static class WindowFrame
{
    /// <summary>Resize margin while restored. Matches WindowChrome.ResizeBorderThickness.</summary>
    public const double Gutter = 8;

    private const int MonitorDefaultToNearest = 0x0002;

    public static void Apply(Window window, FrameworkElement root)
    {
        void Update() => root.Margin = Measure(window);

        window.SourceInitialized += (_, _) => Update();
        window.StateChanged += (_, _) => Update();
        window.SizeChanged += (_, _) => Update();
        window.DpiChanged += (_, _) => Update();
    }

    private static Thickness Measure(Window window)
    {
        if (window.WindowState != WindowState.Maximized)
        {
            return new Thickness(Gutter);
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var bounds)) return default;

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return default;

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return default;

        // Overhang in device pixels, converted to DIPs so the inset stays correct on
        // scaled and mixed-DPI displays.
        var scale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        if (scale <= 0) scale = 1;

        return new Thickness(
            Math.Max(0, info.rcWork.left - bounds.left) / scale,
            Math.Max(0, info.rcWork.top - bounds.top) / scale,
            Math.Max(0, bounds.right - info.rcWork.right) / scale,
            Math.Max(0, bounds.bottom - info.rcWork.bottom) / scale);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor, rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
}
