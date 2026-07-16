using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

public sealed class WindowTrackerService : IWindowTrackerService, IDisposable
{
    private const int PollIntervalMs = 30;
    private static readonly string[] IgnoredClassNames =
    {
        "Shell_TrayWnd", "Progman", "WorkerW", "Windows.UI.Core.CoreWindow"
    };

    private readonly ILogger<WindowTrackerService> _logger;
    private readonly IWindowIdentityService _identity;
    private IntPtr _hostHwnd;
    private IntPtr _draggedHwnd;
    private NativeMethods.WinEventProc? _startHookProc;
    private NativeMethods.WinEventProc? _endHookProc;
    private IntPtr _startHook;
    private IntPtr _endHook;
    private System.Threading.Timer? _pollTimer;

    public event EventHandler<WindowDragEventArgs>? DragStarted;
    public event EventHandler<WindowDragEventArgs>? DragMoved;
    public event EventHandler<WindowDragEventArgs>? DragEnded;

    public WindowTrackerService(IWindowIdentityService identity, ILogger<WindowTrackerService> logger)
    {
        _identity = identity;
        _logger = logger;
    }

    public void Start(IntPtr hostHwnd)
    {
        if (_startHook != IntPtr.Zero || _endHook != IntPtr.Zero) return;
        _hostHwnd = hostHwnd;
        _startHookProc = OnMoveSizeStart;
        _endHookProc = OnMoveSizeEnd;

        _startHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZESTART, NativeMethods.EVENT_SYSTEM_MOVESIZESTART,
            IntPtr.Zero, _startHookProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _endHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZEEND, NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero, _endHookProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _logger.LogInformation("Window drag tracking started");
    }

    public void Stop()
    {
        StopPolling();
        if (_startHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_startHook); _startHook = IntPtr.Zero; }
        if (_endHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_endHook); _endHook = IntPtr.Zero; }
        _startHookProc = null;
        _endHookProc = null;
    }

    // Fires off the UI thread — raw WinEventProc callback.
    private void OnMoveSizeStart(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        if (!IsTrackable(hwnd)) return;
        if (!TryGetRect(hwnd, _identity.GetProcessName(hwnd), out var args)) return;

        _draggedHwnd = hwnd;
        DragStarted?.Invoke(this, args);
        StartPolling();
    }

    // Fires off the UI thread — raw WinEventProc callback.
    private void OnMoveSizeEnd(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        if (hwnd != _draggedHwnd) return;

        StopPolling();
        if (TryGetRect(hwnd, _identity.GetProcessName(hwnd), out var args))
            DragEnded?.Invoke(this, args);

        _draggedHwnd = IntPtr.Zero;
    }

    // Fires off the UI thread — System.Threading.Timer callback.
    private void StartPolling()
    {
        _pollTimer = new System.Threading.Timer(_ =>
        {
            var hwnd = _draggedHwnd;
            if (hwnd == IntPtr.Zero) return;
            if (TryGetRect(hwnd, _identity.GetProcessName(hwnd), out var args))
                DragMoved?.Invoke(this, args);
        }, null, PollIntervalMs, PollIntervalMs);
    }

    private void StopPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private bool IsTrackable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == _hostHwnd) return false;
        if (!NativeMethods.IsWindowVisible(hwnd)) return false;
        if (NativeMethods.IsIconic(hwnd)) return false;

        var className = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(hwnd, className, className.Capacity);
        return Array.IndexOf(IgnoredClassNames, className.ToString()) < 0;
    }

    private static bool TryGetRect(IntPtr hwnd, string processName, out WindowDragEventArgs args)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            args = null!;
            return false;
        }

        args = new WindowDragEventArgs(hwnd, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, processName);
        return true;
    }

    public void Dispose() => Stop();
}
