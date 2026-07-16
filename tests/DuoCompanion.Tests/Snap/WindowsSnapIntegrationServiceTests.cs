using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Snap;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class WindowsSnapIntegrationServiceTests
{
    private static WindowsSnapIntegrationService CreateService() =>
        new(Substitute.For<IWindowSpanService>(), NullLogger<WindowsSnapIntegrationService>.Instance);

    [Fact]
    public void Extend_mode_permits_only_hinge_spans()
    {
        var service = CreateService();

        Assert.True(service.CanAutoSpan(SnapIntegrationMode.ExtendWindowsSnap));
        Assert.False(service.CanApplyLayoutCommand(SnapIntegrationMode.ExtendWindowsSnap));
    }

    [Fact]
    public void Replace_mode_permits_layout_commands()
    {
        var service = CreateService();

        Assert.True(service.CanApplyLayoutCommand(SnapIntegrationMode.ReplaceWindowsSnap));
        Assert.True(service.CanAutoSpan(SnapIntegrationMode.ReplaceWindowsSnap));
    }

    [Fact]
    public void Manually_disabled_mode_requires_explicit_user_selection()
    {
        var service = CreateService();

        Assert.False(service.CanAutoSpan(SnapIntegrationMode.WindowsSnapDisabledManually));
        Assert.True(service.CanApplyLayoutCommand(SnapIntegrationMode.WindowsSnapDisabledManually));
    }

    [Fact]
    public void Destroy_event_forgets_the_window_in_both_span_and_suggestion_services()
    {
        var span = Substitute.For<IWindowSpanService>();
        var suggestions = Substitute.For<ILayoutSuggestionService>();
        var service = new WindowsSnapIntegrationService(
            span, NullLogger<WindowsSnapIntegrationService>.Instance, suggestions);
        var hwnd = new IntPtr(4242);

        // idObject: 0 is ObjIdWindow (the private const the real hook filters on).
        service.OnObjectDestroy(IntPtr.Zero, 0, hwnd, idObject: 0, idChild: 0, idThread: 0, dwmsEventTime: 0);

        span.Received(1).ForgetWindow(hwnd);
        suggestions.Received(1).ForgetWindow(hwnd);
    }

    [Fact]
    public void Destroy_event_still_forgets_the_span_window_when_no_suggestion_service_is_registered()
    {
        // Uses the optional-dependency default (suggestions omitted) to prove
        // OnObjectDestroy doesn't throw when ILayoutSuggestionService isn't
        // supplied, and still forgets the window via IWindowSpanService.
        var span = Substitute.For<IWindowSpanService>();
        var service = new WindowsSnapIntegrationService(span, NullLogger<WindowsSnapIntegrationService>.Instance);
        var hwnd = new IntPtr(99);

        service.OnObjectDestroy(IntPtr.Zero, 0, hwnd, idObject: 0, idChild: 0, idThread: 0, dwmsEventTime: 0);

        span.Received(1).ForgetWindow(hwnd);
    }
}
