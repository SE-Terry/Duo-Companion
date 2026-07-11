using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Window;

public sealed class WindowManagerService : IWindowManagerService, IDisposable
{
    private readonly IDisplayService _display;
    private readonly ISettingsService _settings;
    private readonly ILogger<WindowManagerService> _logger;
    private IntPtr _hook;
    private NativeMethods.WinEventProc? _hookProc; // must hold strong ref — GC can collect delegates passed to unmanaged code

    public event EventHandler? DisplayConfigurationChanged;

    public WindowManagerService(IDisplayService display, ISettingsService settings, ILogger<WindowManagerService> logger)
    {
        _display = display;
        _settings = settings;
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

    public void PositionCompanionWindow(IntPtr hwnd)
    {
        var target = SelectCompanionDisplay();
        if (target is null)
        {
            _logger.LogWarning("No suitable display found — companion window cannot be positioned");
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            target.X, target.Y,
            target.Width, target.Height,
            (uint)NativeMethods.SWP_NOACTIVATE);

        _logger.LogInformation("Companion window positioned at {X},{Y} size {W}x{H}",
            target.X, target.Y, target.Width, target.Height);
    }

    private DisplayInfo? SelectCompanionDisplay()
    {
        var displays = _display.GetAllDisplays();
        if (displays.Count < 2) return null; // single-screen/folded — leave the window where it is, no crash

        return _settings.Current.CompanionDisplay switch
        {
            "Left" => displays.OrderBy(d => d.X).First(),
            "Right" => displays.OrderByDescending(d => d.X).First(),
            _ => displays.FirstOrDefault(d => d.IsSecondary) ?? displays.OrderByDescending(d => d.X).First(),
        };
    }

    private void OnWinEvent(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        _logger.LogInformation("Display configuration changed");
        DisplayConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => StopMonitoring();
}
