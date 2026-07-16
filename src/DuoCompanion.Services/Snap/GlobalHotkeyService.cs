using System.ComponentModel;
using System.Runtime.InteropServices;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

// Owns a hidden message-only window (the "message bridge") solely so RegisterHotKey
// has a stable HWND to post WM_HOTKEY to. The window is created on whichever thread
// calls Start() (the UI thread, per App.xaml.cs) and its WndProc is therefore always
// invoked synchronously on that same thread by the OS's normal message dispatch —
// unlike the out-of-context WinEvent hooks used elsewhere in DuoSnap, no
// DispatcherQueue marshaling is required here, and this service never touches WinUI
// objects directly regardless.
//
// Registration failures — a duplicate binding across two actions, an unparsable
// binding string, or the OS reporting the combination is already owned by another
// application — are never forced. Each is recorded in RegistrationResults and
// logged, leaving that one action's hotkey simply unavailable.
public sealed class GlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private const string ClassName = "DuoCompanion.HotkeyMessageWindow";

    private readonly ISettingsService _settings;
    private readonly IHingeTopologyService _hinge;
    private readonly IWindowSpanService _span;
    private readonly IWindowsSnapIntegrationService _snapIntegration;
    private readonly ILogger<GlobalHotkeyService> _logger;

    private readonly Dictionary<int, WindowLayoutKind> _idToAction = new();
    private readonly List<int> _registeredIds = new();

    private NativeMethods.WndProc? _wndProc;
    private IntPtr _hInstance;
    private ushort _classAtom;
    private IntPtr _messageWindow;

    public IReadOnlyList<HotkeyRegistrationResult> RegistrationResults { get; private set; } =
        Array.Empty<HotkeyRegistrationResult>();

    public event EventHandler<HotkeyInvokedEventArgs>? HotkeyInvoked;

    public GlobalHotkeyService(
        ISettingsService settings,
        IHingeTopologyService hinge,
        IWindowSpanService span,
        IWindowsSnapIntegrationService snapIntegration,
        ILogger<GlobalHotkeyService> logger)
    {
        _settings = settings;
        _hinge = hinge;
        _span = span;
        _snapIntegration = snapIntegration;
        _logger = logger;
    }

    public void Start()
    {
        if (_messageWindow != IntPtr.Zero) return;

        if (!TryCreateMessageWindow())
        {
            _logger.LogError("Failed to create the global hotkey message window; hotkeys are unavailable");
            RegistrationResults = Array.Empty<HotkeyRegistrationResult>();
            return;
        }

        var resolution = HotkeyBindingResolver.Resolve(_settings.Current.DuoSnap.HotkeyBindings);
        var results = new List<HotkeyRegistrationResult>();

        foreach (var error in resolution.Errors)
        {
            results.Add(new HotkeyRegistrationResult(error.Action, error.Text, succeeded: false, error.Reason));
            _logger.LogWarning(
                "Hotkey binding for {Action} rejected: {Reason}", error.Action, error.Reason);
        }

        var nextId = 1;
        foreach (var (action, binding) in resolution.Bindings)
        {
            var id = nextId++;
            var fsModifiers = (uint)binding.Modifiers | NativeMethods.MOD_NOREPEAT;

            if (NativeMethods.RegisterHotKey(_messageWindow, id, fsModifiers, binding.VirtualKey))
            {
                _idToAction[id] = action;
                _registeredIds.Add(id);
                results.Add(new HotkeyRegistrationResult(action, binding.Text, succeeded: true));
                _logger.LogInformation("Registered hotkey {Binding} for {Action}", binding.Text, action);
            }
            else
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                results.Add(new HotkeyRegistrationResult(action, binding.Text, succeeded: false, error));
                _logger.LogWarning(
                    "Failed to register hotkey {Binding} for {Action}: {Error}", binding.Text, action, error);
            }
        }

        RegistrationResults = results;
        _logger.LogInformation("Global hotkey service started with {Count} registered hotkey(s)", _registeredIds.Count);
    }

    public void Stop()
    {
        if (_messageWindow == IntPtr.Zero) return;

        foreach (var id in _registeredIds)
            NativeMethods.UnregisterHotKey(_messageWindow, id);
        _registeredIds.Clear();
        _idToAction.Clear();

        NativeMethods.DestroyWindow(_messageWindow);
        _messageWindow = IntPtr.Zero;

        if (_classAtom != 0)
        {
            NativeMethods.UnregisterClass(ClassName, _hInstance);
            _classAtom = 0;
        }

        // Safe to release only after DestroyWindow — the OS may still dispatch
        // messages (e.g. WM_DESTROY) through lpfnWndProc up to that point.
        _wndProc = null;
        RegistrationResults = Array.Empty<HotkeyRegistrationResult>();
        _logger.LogInformation("Global hotkey service stopped");
    }

    private bool TryCreateMessageWindow()
    {
        _hInstance = NativeMethods.GetModuleHandle(null);
        _wndProc = WndProc;

        var wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = _wndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = _hInstance,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = ClassName,
            hIconSm = IntPtr.Zero,
        };

        _classAtom = NativeMethods.RegisterClassEx(ref wndClass);
        if (_classAtom == 0)
        {
            _logger.LogWarning(
                "RegisterClassEx failed: {Error}", new Win32Exception(Marshal.GetLastWin32Error()).Message);
            _wndProc = null;
            return false;
        }

        _messageWindow = NativeMethods.CreateWindowEx(
            0, ClassName, null, 0, 0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE, IntPtr.Zero, _hInstance, IntPtr.Zero);

        if (_messageWindow == IntPtr.Zero)
        {
            _logger.LogWarning(
                "CreateWindowEx failed: {Error}", new Win32Exception(Marshal.GetLastWin32Error()).Message);
            NativeMethods.UnregisterClass(ClassName, _hInstance);
            _classAtom = 0;
            _wndProc = null;
            return false;
        }

        return true;
    }

    // Invoked by the OS's normal message dispatch on the thread that called Start().
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            OnHotkeyMessage((int)wParam);
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void OnHotkeyMessage(int id)
    {
        if (!_idToAction.TryGetValue(id, out var action)) return;

        HotkeyInvoked?.Invoke(this, new HotkeyInvokedEventArgs(action));

        var settings = _settings.Current.DuoSnap;
        if (!_snapIntegration.CanApplyLayoutCommand(settings.SnapIntegrationMode))
        {
            _logger.LogInformation(
                "Hotkey for {Action} ignored — layout commands are disallowed by the current Snap integration mode",
                action);
            return;
        }

        var topology = _hinge.CurrentTopology;
        if (topology is null)
        {
            _logger.LogInformation("Hotkey for {Action} ignored — hinge topology is currently ambiguous", action);
            return;
        }

        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        var target = WindowLayoutCalculator.Compute(topology, action);
        _span.ApplyLayout(hwnd, target);
        _logger.LogInformation("Applied hotkey layout {Action} to foreground window {Hwnd}", action, hwnd);
    }

    public void Dispose() => Stop();
}
