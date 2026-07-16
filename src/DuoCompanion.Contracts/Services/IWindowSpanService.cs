using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IWindowSpanService
{
    bool IsSpanned(IntPtr hwnd);
    void Span(IntPtr hwnd, SpanTarget target);
    void Restore(IntPtr hwnd);
    void ApplyLayout(IntPtr hwnd, WindowLayoutTarget target);
    void ForgetWindow(IntPtr hwnd);
}
