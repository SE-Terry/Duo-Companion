using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

public sealed class WindowSpanService : IWindowSpanService
{
    private readonly Dictionary<IntPtr, SpanTarget> _originalRects = new();
    private readonly Dictionary<IntPtr, SpanTarget> _layoutOriginalRects = new();
    private readonly ILogger<WindowSpanService> _logger;

    public WindowSpanService(ILogger<WindowSpanService> logger) => _logger = logger;

    public bool IsSpanned(IntPtr hwnd) => _originalRects.ContainsKey(hwnd);

    public void Span(IntPtr hwnd, SpanTarget target)
    {
        if (!_originalRects.ContainsKey(hwnd))
        {
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
            _originalRects[hwnd] = new SpanTarget(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        Move(hwnd, target);
        _logger.LogInformation("Spanned window {Hwnd} to {Target}", hwnd, target);
    }

    public void Restore(IntPtr hwnd)
    {
        if (!_originalRects.TryGetValue(hwnd, out var original)) return;

        Move(hwnd, original);
        _originalRects.Remove(hwnd);
        _logger.LogInformation("Restored window {Hwnd} to {Original}", hwnd, original);
    }

    public void ApplyLayout(IntPtr hwnd, WindowLayoutTarget target)
    {
        if (!_layoutOriginalRects.ContainsKey(hwnd))
        {
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
            _layoutOriginalRects[hwnd] = new SpanTarget(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        Move(hwnd, new SpanTarget(target.Left, target.Top, target.Width, target.Height));
        _logger.LogInformation("Applied layout to window {Hwnd}: {Target}", hwnd, target);
    }

    public void ForgetWindow(IntPtr hwnd)
    {
        var forgotSpan = _originalRects.Remove(hwnd);
        var forgotLayout = _layoutOriginalRects.Remove(hwnd);
        if (forgotSpan || forgotLayout)
            _logger.LogDebug("Forgot stored restore rectangle for destroyed window {Hwnd}", hwnd);
    }

    private static void Move(IntPtr hwnd, SpanTarget target) =>
        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero,
            target.Left, target.Top, target.Width, target.Height,
            (uint)(NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE));
}
