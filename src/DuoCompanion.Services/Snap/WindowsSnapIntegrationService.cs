using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

// Owns Windows Snap integration-mode policy and the EVENT_OBJECT_DESTROY hook
// that discards stored restore rectangles for windows that no longer exist.
// This service never reads or mutates any Windows Snap OS setting in any mode:
//   - ExtendWindowsSnap only offers the hinge-drag auto span; normal Windows
//     Snap (Left/Right/quarter, etc.) is left untouched, and DuoCompanion
//     never offers a competing custom layout command in this mode.
//   - ReplaceWindowsSnap additionally enables DuoCompanion's own custom
//     layout commands (hotkeys/menu). "Enable" means DuoCompanion will act on
//     them — it is still just enabling our own commands, not touching any OS
//     setting.
//   - WindowsSnapDisabledManually is informational: the user has already
//     turned Windows Snap off themselves. DuoCompanion still never touches
//     that OS setting; it simply stops offering automatic (dwell-triggered)
//     spanning and requires every layout to be an explicit user selection.
public sealed class WindowsSnapIntegrationService : IWindowsSnapIntegrationService, IDisposable
{
    private const int ObjIdWindow = 0;

    private readonly IWindowSpanService _span;
    private readonly ILogger<WindowsSnapIntegrationService> _logger;
    private readonly ILayoutSuggestionService? _suggestions;
    private NativeMethods.WinEventProc? _destroyHookProc;
    private IntPtr _destroyHook;

    // suggestions is optional (null when not registered in DI yet) for the same
    // reason AutoSpanCoordinatorService's profiles/suggestions parameters are
    // optional — so existing callers/tests built around the original
    // two-argument constructor keep compiling and behaving exactly as before.
    public WindowsSnapIntegrationService(
        IWindowSpanService span, ILogger<WindowsSnapIntegrationService> logger,
        ILayoutSuggestionService? suggestions = null)
    {
        _span = span;
        _logger = logger;
        _suggestions = suggestions;
    }

    public bool CanAutoSpan(SnapIntegrationMode mode) =>
        mode != SnapIntegrationMode.WindowsSnapDisabledManually;

    public bool CanApplyLayoutCommand(SnapIntegrationMode mode) =>
        mode != SnapIntegrationMode.ExtendWindowsSnap;

    public void Start()
    {
        if (_destroyHook != IntPtr.Zero) return;

        _destroyHookProc = OnObjectDestroy;
        _destroyHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_DESTROY,
            IntPtr.Zero, _destroyHookProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _logger.LogInformation("Windows Snap integration lifecycle started");
    }

    public void Stop()
    {
        if (_destroyHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_destroyHook);
            _destroyHook = IntPtr.Zero;
        }

        _destroyHookProc = null;
    }

    // Fires off the UI thread — raw WinEventProc callback. Internal (rather than
    // private) so tests can invoke it directly without going through a real
    // SetWinEventHook registration, matching this codebase's existing convention
    // of using `internal` as a test seam (see SettingsService's internal ctor).
    internal void OnObjectDestroy(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwmsEventTime)
    {
        if (idObject != ObjIdWindow || hwnd == IntPtr.Zero) return;
        _span.ForgetWindow(hwnd);
        _suggestions?.ForgetWindow(hwnd);
    }

    public void Dispose() => Stop();
}
