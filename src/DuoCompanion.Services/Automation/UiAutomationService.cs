using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;
using UIAutomationClient;

namespace DuoCompanion.Services.Automation;

public sealed class UiAutomationService : IUiAutomationService, IDisposable
{
    // UIA_ControlTypeIds: https://learn.microsoft.com/windows/win32/winauto/uiauto-controltype-ids
    private const int UIA_EditControlTypeId = 50004;
    private const int UIA_DocumentControlTypeId = 50030;

    private readonly ILogger<UiAutomationService> _logger;
    private IntPtr _hostHwnd;
    private IUIAutomation? _automation;
    private IUIAutomationFocusChangedEventHandler? _focusHandler;

    public event EventHandler? TextInputFocused;
    public event EventHandler? TextInputBlurred;

    public UiAutomationService(ILogger<UiAutomationService> logger) => _logger = logger;

    public void Start(IntPtr hostHwnd)
    {
        _hostHwnd = hostHwnd;
        _automation = new CUIAutomation();
        _focusHandler = new FocusChangedHandler(this);
        _automation.AddFocusChangedEventHandler(null, _focusHandler);

        _logger.LogInformation("UI Automation focus monitoring started");
    }

    public void Stop()
    {
        if (_automation is not null && _focusHandler is not null)
            _automation.RemoveFocusChangedEventHandler(_focusHandler);

        _focusHandler = null;
        _automation = null;
    }

    // UIA delivers focus-changed callbacks on its own background thread, not the UI
    // thread — safe here since it only reads the element and raises events; callers
    // (CompanionWindow) marshal back via DispatcherQueue themselves.
    private void OnFocusChanged(IUIAutomationElement? element)
    {
        if (element is null) return;

        try
        {
            // CurrentNativeWindowHandle is declared as a 32-bit int by the UIA type
            // library even on 64-bit Windows; real window handles fit in it in practice.
            var hwnd = new IntPtr(element.CurrentNativeWindowHandle);
            if (hwnd != IntPtr.Zero && NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT) == _hostHwnd)
                return;

            var controlType = element.CurrentControlType;
            if (controlType is UIA_EditControlTypeId or UIA_DocumentControlTypeId)
            {
                _logger.LogDebug("Text input focused (controlType={ControlType})", controlType);
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
            TextInputBlurred?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => Stop();

    private sealed class FocusChangedHandler : IUIAutomationFocusChangedEventHandler
    {
        private readonly UiAutomationService _owner;

        public FocusChangedHandler(UiAutomationService owner) => _owner = owner;

        public void HandleFocusChangedEvent(IUIAutomationElement sender) =>
            _owner.OnFocusChanged(sender);
    }
}
