using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Window;

public sealed class WindowManagerService : IWindowManagerService, IDisposable
{
    private readonly IDisplayService _display;
    private readonly ILogger<WindowManagerService> _logger;
    private IntPtr _hook;
    private NativeMethods.WinEventProc? _hookProc; // must hold strong ref — GC can collect delegates passed to unmanaged code

    public event EventHandler? DisplayConfigurationChanged;

    public WindowManagerService(IDisplayService display, ILogger<WindowManagerService> logger)
    {
        _display = display;
        _logger = logger;
    }

    public void StartMonitoring(IntPtr hostHwnd)
    {
        _ = hostHwnd; // process-wide hook; parameter reserved for future per-window filtering
        _hookProc = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_DISPLAYCHANGE,
            NativeMethods.EVENT_SYSTEM_DISPLAYCHANGE,
            IntPtr.Zero, _hookProc,
            0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _logger.LogInformation("Display change monitoring started");
    }

    public void StopMonitoring()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
        _hookProc = null;
    }

    public void PositionOnSecondaryDisplay(IntPtr hwnd)
    {
        var secondary = _display.GetSecondaryDisplay();
        if (secondary is null)
        {
            _logger.LogWarning("No secondary display found — companion window cannot be positioned");
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            secondary.X, secondary.Y,
            secondary.Width, secondary.Height,
            (uint)NativeMethods.SWP_NOACTIVATE);

        _logger.LogInformation("Companion window positioned at {X},{Y} size {W}x{H}",
            secondary.X, secondary.Y, secondary.Width, secondary.Height);
    }

    private void OnWinEvent(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        _logger.LogInformation("Display configuration changed");
        DisplayConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => StopMonitoring();
}
