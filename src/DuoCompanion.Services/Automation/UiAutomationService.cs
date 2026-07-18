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
    private static readonly TimeSpan SuppressionWindow = TimeSpan.FromMilliseconds(400);

    private readonly ILogger<UiAutomationService> _logger;
    private IntPtr _hostHwnd;
    private IntPtr _lastTextFieldRootHwnd;
    private DateTime _suppressUntilUtc;
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

    public void SuppressBriefly() => _suppressUntilUtc = DateTime.UtcNow + SuppressionWindow;

    // UIA delivers focus-changed callbacks on its own background thread, not the UI
    // thread — safe here since it only reads the element and raises events; callers
    // (CompanionWindow) marshal back via DispatcherQueue themselves.
    private void OnFocusChanged(IUIAutomationElement? element)
    {
        if (element is null || DateTime.UtcNow < _suppressUntilUtc) return;

        try
        {
            // CurrentNativeWindowHandle is declared as a 32-bit int by the UIA type
            // library even on 64-bit Windows; real window handles fit in it in practice.
            var hwnd = new IntPtr(element.CurrentNativeWindowHandle);
            var root = hwnd != IntPtr.Zero ? NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT) : IntPtr.Zero;

            if (root != IntPtr.Zero && root == _hostHwnd) return; // ignore our own UI

            var controlType = element.CurrentControlType;
            if (controlType is UIA_EditControlTypeId or UIA_DocumentControlTypeId)
            {
                _logger.LogDebug("Text input focused (controlType={ControlType})", controlType);
                _lastTextFieldRootHwnd = root;
                TextInputFocused?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Any focus target that isn't an edit/document control counts as blur,
            // regardless of whether it's in the same top-level window as the last
            // text field — a click on a button or empty space in the same app window
            // is a legitimate "the user is done with this field" signal and must not
            // be suppressed. The transient same-window noise this guard used to filter
            // (autocomplete popups, caret helpers, per-keystroke re-validation in rich
            // editors/WebView2) is instead absorbed by CompanionWindow's hide debounce:
            // a fresh TextInputFocused cancels the pending hide before it fires.
            if (root == IntPtr.Zero) return;

            TextInputBlurred?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking focused element");
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
