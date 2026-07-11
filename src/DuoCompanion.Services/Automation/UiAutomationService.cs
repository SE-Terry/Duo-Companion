using System.Runtime.InteropServices;
using Accessibility;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Automation;

public sealed class UiAutomationService : IUiAutomationService, IDisposable
{
    private readonly ILogger<UiAutomationService> _logger;
    private IntPtr _focusHook;
    private NativeMethods.WinEventProc? _hookProc; // keep alive — GC can collect delegates

    public event EventHandler? TextInputFocused;
    public event EventHandler? TextInputBlurred;

    public UiAutomationService(ILogger<UiAutomationService> logger) => _logger = logger;

    public void Start()
    {
        _hookProc = OnFocusEvent;
        _focusHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_FOCUS,
            NativeMethods.EVENT_OBJECT_FOCUS,
            IntPtr.Zero, _hookProc,
            0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _logger.LogInformation("UI Automation focus monitoring started");
    }

    public void Stop()
    {
        if (_focusHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_focusHook);
            _focusHook = IntPtr.Zero;
        }
        _hookProc = null;
    }

    private void OnFocusEvent(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        // Must run on this (STA) thread — IAccessible COM objects are apartment-affine,
        // and calling them from a ThreadPool thread without CoInitializeEx crashes natively.
        CheckFocusedElement(hwnd, idObject, idChild);
    }

    private void CheckFocusedElement(IntPtr hwnd, int idObject, int idChild)
    {
        try
        {
            var hr = NativeMethods.AccessibleObjectFromEvent(hwnd, idObject, idChild,
                out var accObj, out _);

            if (hr != 0 || accObj is not Accessibility.IAccessible acc) return;

            object roleVariant = acc.get_accRole(idChild);
            var role = Convert.ToUInt32(roleVariant);

            // ROLE_SYSTEM_TEXT = 42, ROLE_SYSTEM_DOCUMENT = 15
            if (role is 42 or 15)
            {
                _logger.LogDebug("Text input focused (role={Role})", role);
                TextInputFocused?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                TextInputBlurred?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking focused element");
        }
    }

    public void Dispose() => Stop();
}
