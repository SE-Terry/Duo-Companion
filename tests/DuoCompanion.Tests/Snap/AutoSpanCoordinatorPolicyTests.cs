using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Snap;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class AutoSpanCoordinatorPolicyTests
{
    private static readonly IntPtr Window = new(42);
    private static readonly DuoDisplayTopology ValidTopology = new(
        new HingeZone(
            new DisplayInfo(0, "LEFT", 0, 0, 1350, 1800, true),
            new DisplayInfo(1, "RIGHT", 1350, 0, 1350, 1800, false),
            IsVertical: true,
            ActivationCenter: 1350,
            ActivationHalfWidth: 30),
        HasExternalDisplays: false);

    [Fact]
    public void Drag_end_does_not_span_when_the_topology_is_ambiguous()
    {
        var fixture = CreateFixture(hasTopology: false);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded();

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_does_not_span_an_ignored_executable()
    {
        var fixture = CreateFixture(ignoredExecutableNames: ["notepad"]);

        fixture.RaiseDragStarted(processName: "NOTEPAD");
        fixture.RaiseDragMoved(processName: "NOTEPAD");
        fixture.RaiseDragEnded(processName: "NOTEPAD");

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_does_not_span_until_the_dwell_interval_has_elapsed()
    {
        var fixture = CreateFixture(dwellDurationMilliseconds: 1000);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded();

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Settings_change_that_disables_auto_span_cancels_an_active_preview()
    {
        var fixture = CreateFixture();
        var exits = 0;
        fixture.Coordinator.SpanCandidateExited += (_, _) => exits++;

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.Settings.Current.DuoSnap.AutoSpanEnabled = false;
        fixture.RaiseSettingsChanged();

        Assert.Equal(1, exits);
    }

    [Fact]
    public void Disabling_auto_span_during_candidacy_produces_exactly_one_exit_and_never_spans()
    {
        // Acceptance test at the coordinator's outermost boundary: a candidate
        // preview is showing, the user (or Settings UI) disables Auto Span before
        // releasing the drag, and the drag then completes. Exactly one
        // SpanCandidateExited must fire (from the settings-change cleanup) and
        // Span.Span must never be called for the drag that completes afterward.
        // Note: DuoSnapPolicy.CanSpan also independently gates on AutoSpanEnabled,
        // so this does not in isolation prove OnSettingsChanged's ClearCandidate()/
        // ClearGesture() state-reset is what blocks the span — it proves the
        // end-to-end "disable mid-drag never produces a spurious span" behavior.
        var fixture = CreateFixture();
        var exits = 0;
        fixture.Coordinator.SpanCandidateExited += (_, _) => exits++;

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.Settings.Current.DuoSnap.AutoSpanEnabled = false;
        fixture.RaiseSettingsChanged();
        fixture.RaiseDragEnded();

        Assert.Equal(1, exits);
        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Never_restore_behavior_does_not_restore_a_spanned_window_on_drag_start()
    {
        var fixture = CreateFixture(restoreBehavior: RestoreBehavior.Never);
        fixture.Span.IsSpanned(Window).Returns(true);

        fixture.RaiseDragStarted();

        fixture.Span.DidNotReceive().Restore(Window);
    }

    [Fact]
    public void Drag_end_does_not_span_when_the_snap_integration_mode_disallows_auto_span()
    {
        var fixture = CreateFixture(canAutoSpan: false);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded();

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_applies_a_matching_profile_instead_of_auto_spanning()
    {
        // canApplyLayoutCommand: true and default topology/dwell/ignore settings
        // mean auto-span would otherwise fire (canSpan would be true) for this
        // in-hinge-zone drag — the profile must win and Span.Span must never be
        // called, proving profile precedence over auto-span at the coordinator's
        // composition level (not just within AppLayoutProfileService in isolation).
        var profiles = Substitute.For<IAppLayoutProfileService>();
        profiles.ApplyIfMatched(Window, "notepad").Returns(true);
        var fixture = CreateFixture(canApplyLayoutCommand: true, profiles: profiles);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded();

        profiles.Received(1).ApplyIfMatched(Window, "notepad");
        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_does_not_apply_a_profile_when_the_drop_is_outside_the_hinge_zone()
    {
        // Same matching profile and canApplyLayoutCommand as the precedence test
        // above, but the drop lands far outside ValidTopology's hinge zone
        // ([1320, 1380]). Regression guard for the hinge-zone gate itself: without
        // it, ApplyIfMatched would fire on any drag anywhere on screen.
        var profiles = Substitute.For<IAppLayoutProfileService>();
        profiles.ApplyIfMatched(Window, "notepad").Returns(true);
        var fixture = CreateFixture(canApplyLayoutCommand: true, profiles: profiles);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded(left: 100, top: 100);

        profiles.DidNotReceive().ApplyIfMatched(Arg.Any<IntPtr>(), Arg.Any<string?>());
    }

    [Fact]
    public void Drag_end_confirms_a_title_bar_hold_gesture_when_auto_span_is_disallowed_by_mode()
    {
        // canAutoSpan: false mirrors WindowsSnapDisabledManually — ordinary
        // drag-through auto-span is off (canSpan will be false), but
        // canApplyLayoutCommand: true means the explicit title-bar dwell gesture
        // must still be able to confirm a span. dwellDurationMilliseconds is left
        // at the fixture default (0) and a real sleep past
        // AutoSpanCoordinatorService.GestureMinimumHoldMilliseconds is used
        // instead, so this proves an actually-held gesture confirms even when
        // the configured dwell setting is 0 — the gesture's own minimum applies
        // regardless of the configured dwell.
        var fixture = CreateFixture(canAutoSpan: false, canApplyLayoutCommand: true);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        Thread.Sleep(AutoSpanCoordinatorService.GestureMinimumHoldMilliseconds + 150);
        fixture.RaiseDragEnded();

        fixture.Span.Received(1).Span(Window, Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_does_not_confirm_the_title_bar_gesture_for_a_momentary_pass_through_the_hinge()
    {
        // Regression guard for the bug this finding fixes: with
        // dwellDurationMilliseconds at its 0ms default (or any low value), an
        // ordinary drag that merely passes through/ends in the hinge zone
        // without being held there for GestureMinimumHoldMilliseconds must NOT
        // instantly confirm the gesture — otherwise WindowsSnapDisabledManually
        // (canAutoSpan: false here) would span on every ordinary drop near the
        // hinge, contradicting "layout-commands-only, no drag-through auto-span".
        // No sleep between moved/ended — real elapsed time here is microseconds,
        // far under the 400ms gesture minimum.
        var fixture = CreateFixture(canAutoSpan: false, canApplyLayoutCommand: true);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded();

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_does_not_confirm_the_title_bar_gesture_before_dwell_completes()
    {
        var fixture = CreateFixture(
            canAutoSpan: false, canApplyLayoutCommand: true, dwellDurationMilliseconds: 60_000);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded();

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_does_not_confirm_the_title_bar_gesture_when_layout_commands_are_disallowed()
    {
        var fixture = CreateFixture(canAutoSpan: false, canApplyLayoutCommand: false);

        fixture.RaiseDragStarted();
        fixture.RaiseDragMoved();
        fixture.RaiseDragEnded();

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    [Fact]
    public void Drag_end_does_not_confirm_the_title_bar_gesture_for_an_ignored_executable()
    {
        // Same canAutoSpan:false/canApplyLayoutCommand:true shape as
        // Drag_end_confirms_a_title_bar_hold_gesture_when_auto_span_is_disallowed_by_mode
        // (where the gesture DOES confirm a span), but "notepad" is on the ignore
        // list here. The gesture must never bypass the ignore-list gate that
        // ordinary auto-span already respects (DuoSnapPolicy.CanSpan) — an
        // explicitly excluded executable must stay excluded regardless of which
        // path (drag-through auto-span or title-bar hold gesture) confirms Span.
        var fixture = CreateFixture(
            canAutoSpan: false, canApplyLayoutCommand: true, ignoredExecutableNames: ["notepad"]);

        fixture.RaiseDragStarted(processName: "NOTEPAD");
        fixture.RaiseDragMoved(processName: "NOTEPAD");
        fixture.RaiseDragEnded(processName: "NOTEPAD");

        fixture.Span.DidNotReceive().Span(Arg.Any<IntPtr>(), Arg.Any<SpanTarget>());
    }

    private static CoordinatorFixture CreateFixture(
        bool hasTopology = true,
        int dwellDurationMilliseconds = 0,
        RestoreBehavior restoreBehavior = RestoreBehavior.OnNextDrag,
        List<string>? ignoredExecutableNames = null,
        bool canAutoSpan = true,
        bool canApplyLayoutCommand = false,
        IAppLayoutProfileService? profiles = null)
    {
        var tracker = Substitute.For<IWindowTrackerService>();
        var hinge = Substitute.For<IHingeTopologyService>();
        var span = Substitute.For<IWindowSpanService>();
        var settings = Substitute.For<ISettingsService>();
        var snapIntegration = Substitute.For<IWindowsSnapIntegrationService>();
        snapIntegration.CanAutoSpan(Arg.Any<SnapIntegrationMode>()).Returns(canAutoSpan);
        snapIntegration.CanApplyLayoutCommand(Arg.Any<SnapIntegrationMode>()).Returns(canApplyLayoutCommand);
        settings.Current.Returns(new AppSettings
        {
            DuoSnap = new DuoSnapSettings
            {
                DwellDurationMilliseconds = dwellDurationMilliseconds,
                RestoreBehavior = restoreBehavior,
                IgnoredExecutableNames = ignoredExecutableNames ?? []
            }
        });
        hinge.CurrentTopology.Returns(hasTopology ? ValidTopology : null);

        var fixture = new CoordinatorFixture(
            tracker,
            hinge,
            span,
            settings,
            new AutoSpanCoordinatorService(tracker, hinge, span, settings, snapIntegration,
                NullLogger<AutoSpanCoordinatorService>.Instance, profiles));
        fixture.Coordinator.Start(IntPtr.Zero);
        return fixture;
    }

    private sealed record CoordinatorFixture(
        IWindowTrackerService Tracker,
        IHingeTopologyService Hinge,
        IWindowSpanService Span,
        ISettingsService Settings,
        AutoSpanCoordinatorService Coordinator)
    {
        public void RaiseDragStarted(string processName = "notepad") =>
            Tracker.DragStarted += Raise.Event<EventHandler<WindowDragEventArgs>>(
                this, new WindowDragEventArgs(Window, 1325, 0, 50, 50, processName));

        public void RaiseDragMoved(string processName = "notepad") =>
            Tracker.DragMoved += Raise.Event<EventHandler<WindowDragEventArgs>>(
                this, new WindowDragEventArgs(Window, 1325, 0, 50, 50, processName));

        public void RaiseDragEnded(string processName = "notepad", int left = 1325, int top = 0) =>
            Tracker.DragEnded += Raise.Event<EventHandler<WindowDragEventArgs>>(
                this, new WindowDragEventArgs(Window, left, top, 50, 50, processName));

        public void RaiseSettingsChanged() =>
            Settings.SettingsChanged += Raise.Event<EventHandler>(this, EventArgs.Empty);
    }
}
