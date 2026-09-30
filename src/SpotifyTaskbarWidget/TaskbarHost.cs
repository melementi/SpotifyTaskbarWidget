using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using static SpotifyTaskbarWidget.NativeMethods;

namespace SpotifyTaskbarWidget;

internal sealed class TaskbarHost(Window window, int maxWidth)
{
    private const int LeftMargin = 12;
    private const int MinWidth = 260;

    private IntPtr hwnd;
    private bool fits = true;

    public void Attach()
    {
        hwnd = new WindowInteropHelper(window).Handle;
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
        Place();
    }

    public void Update()
    {
        var taskbar = Place();
        if (taskbar == IntPtr.Zero) return;
        window.Visibility = fits && !FullscreenCoversMonitorOf(taskbar) ? Visibility.Visible : Visibility.Hidden;
    }

    private IntPtr Place()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero) return taskbar;

        var reowned = GetWindowLongPtr(hwnd, GWLP_HWNDPARENT) != taskbar;
        if (reowned)
        {
            // An owned window stays above its owner, so clicking the taskbar cannot cover the widget.
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, taskbar);
        }

        GetWindowRect(taskbar, out var bar);
        var scale = GetDpiForWindow(taskbar) / 96.0;
        var left = bar.Left + (int)(LeftMargin * scale);
        var width = Math.Min((int)(maxWidth * scale), FreeWidth(taskbar, left, scale));
        var height = bar.Bottom - bar.Top;
        // A left-aligned taskbar, or one crowded with apps, leaves no room left of the Start button.
        fits = width >= (int)(MinWidth * scale);

        GetWindowRect(hwnd, out var current);
        var moved = current.Left != left || current.Top != bar.Top || current.Right - current.Left != width || current.Bottom - current.Top != height;
        // Ownership alone does not keep the widget on top: explorer raises the taskbar without its owned
        // windows, and a new owner after an explorer restart does not reorder anything.
        if (fits && (reowned || moved || IsBelow(taskbar)))
            SetWindowPos(hwnd, JustAbove(taskbar), left, bar.Top, width, height, SWP_NOACTIVATE);

        return taskbar;
    }

    private static int FreeWidth(IntPtr taskbar, int left, double scale)
    {
        // Windows 11 keeps a legacy "Start" child window that tracks the Start button's position.
        var start = FindWindowEx(taskbar, IntPtr.Zero, "Start", null);
        if (start == IntPtr.Zero || !GetWindowRect(start, out var button)) return int.MaxValue;
        return button.Left - left - (int)(LeftMargin * scale);
    }

    // Inserting directly above the taskbar, rather than at the top of the topmost band, keeps
    // Start, jump lists and other shell popups above the widget.
    private static IntPtr JustAbove(IntPtr taskbar)
    {
        var above = GetWindow(taskbar, GW_HWNDPREV);
        return above == IntPtr.Zero ? HWND_TOPMOST : above;
    }

    private bool IsBelow(IntPtr other)
    {
        for (var above = GetWindow(hwnd, GW_HWNDPREV); above != IntPtr.Zero; above = GetWindow(above, GW_HWNDPREV))
        {
            if (above == other) return true;
        }
        return false;
    }

    private bool FullscreenCoversMonitorOf(IntPtr taskbar)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == hwnd || foreground == taskbar) return false;

        var name = new StringBuilder(64);
        GetClassName(foreground, name, name.Capacity);
        // The desktop and Task View span the whole monitor but keep the taskbar visible.
        if (name.ToString() is "Progman" or "WorkerW" or "XamlExplorerHostIslandWindow") return false;

        var monitor = MonitorFromWindow(foreground, MONITOR_DEFAULTTONEAREST);
        if (monitor != MonitorFromWindow(taskbar, MONITOR_DEFAULTTONEAREST)) return false;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        GetWindowRect(foreground, out var rect);
        return rect.Left <= info.rcMonitor.Left
            && rect.Top <= info.rcMonitor.Top
            && rect.Right >= info.rcMonitor.Right
            && rect.Bottom >= info.rcMonitor.Bottom;
    }
}
