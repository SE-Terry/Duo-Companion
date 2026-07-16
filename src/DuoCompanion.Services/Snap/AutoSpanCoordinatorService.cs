using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DuoCompanion.Services.Snap;

public sealed class AutoSpanCoordinatorService : IAutoSpanCoordinatorService, IDisposable
{
    // The title-bar hold gesture must never be satisfiable by an ordinary,
    // momentary drag-and-drop into the hinge zone — otherwise
    // DwellDurationMilliseconds defaulting to 0 (or being configured low for
    // snappy ordinary auto-span) would let any drop near the hinge confirm the
    // gesture instantly, even in modes (WindowsSnapDisabledManually, or
    // AutoSpanEnabled off) that document "no drag-through auto-span, layout
    // commands only". 400ms is comfortably longer than an incidental pass
    // through the hinge on the way to a drop elsewhere, but still short enough
    // that a deliberate hold doesn't feel unresponsive. IsGestureDwellComplete
    // takes the max of this and the configured DwellDurationMilliseconds, so an
    // explicitly configured longer dwell is still honored.
    internal const int GestureMinimumHoldMilliseconds = 400;

    private readonly IWindowTrackerService _tracker;
    private readonly IHingeTopologyService _hinge;
    private readonly IWindowSpanService _span;
    private readonly ISettingsService _settings;
    private readonly IWindowsSnapIntegrationService _snapIntegration;
    private readonly IAppLayoutProfileService? _profiles;
    private readonly ILayoutSuggestionService? _suggestions;
    private readonly ILogger<AutoSpanCoordinatorService> _logger;
    private bool _isStarted;
    private IntPtr _draggedHwnd;
    private IntPtr _candidateHwnd;
    private long _candidateStartedAt;
    private bool _previewVisible;
    private IntPtr _gestureHwnd;
    private long _gestureEnteredAt;

    public event EventHandler<SpanCandidateEventArgs>? SpanCandidateEntered;
    public event EventHandler? SpanCandidateExited;

    // profiles/suggestions are optional (null when not registered in DI yet) so
    // that existing callers/tests built around the original six-argument
    // constructor keep compiling and behaving exactly as before.
    public AutoSpanCoordinatorService(
        IWindowTrackerService tracker,
        IHingeTopologyService hinge,
        IWindowSpanService span,
        ISettingsService settings,
        IWindowsSnapIntegrationService snapIntegration,
        ILogger<AutoSpanCoordinatorService> logger,
        IAppLayoutProfileService? profiles = null,
        ILayoutSuggestionService? suggestions = null)
    {
        _tracker = tracker;
        _hinge = hinge;
        _span = span;
        _settings = settings;
        _snapIntegration = snapIntegration;
        _logger = logger;
        _profiles = profiles;
        _suggestions = suggestions;
    }

    public void Start(IntPtr hostHwnd)
    {
        if (_isStarted) return;
        _isStarted = true;

        _hinge.TopologyChanged += OnTopologyChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        _tracker.DragStarted += OnDragStarted;
        _tracker.DragMoved += OnDragMoved;
        _tracker.DragEnded += OnDragEnded;
        _hinge.Start();
        _tracker.Start(hostHwnd);
        _snapIntegration.Start();
        _logger.LogInformation("Auto-span coordinator started");
    }

    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;

        ClearCandidate();
        ClearGesture();
        _draggedHwnd = IntPtr.Zero;
        _tracker.DragStarted -= OnDragStarted;
        _tracker.DragMoved -= OnDragMoved;
        _tracker.DragEnded -= OnDragEnded;
        _hinge.TopologyChanged -= OnTopologyChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
        _tracker.Stop();
        _hinge.Stop();
        _snapIntegration.Stop();
    }

    private void OnTopologyChanged(object? sender, EventArgs e)
    {
        ClearCandidate();
        ClearGesture();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        ClearCandidate();
        ClearGesture();
    }

    private void OnDragStarted(object? sender, WindowDragEventArgs e)
    {
        if (!_isStarted) return;

        ClearCandidate();
        _draggedHwnd = e.Hwnd;

        var settings = _settings.Current.DuoSnap;
        if (!settings.AutoSpanEnabled) return;
        if (settings.RestoreBehavior == RestoreBehavior.OnNextDrag && _span.IsSpanned(e.Hwnd))
            _span.Restore(e.Hwnd);
    }

    private void OnDragMoved(object? sender, WindowDragEventArgs e)
    {
        if (!_isStarted || e.Hwnd != _draggedHwnd) return;

        var settings = _settings.Current.DuoSnap;
        var topology = _hinge.CurrentTopology;
        var inHingeZone = topology is not null && topology.Hinge.Contains(e.CenterX, e.CenterY);

        // Tracked independently of AutoSpanEnabled/ignore-list/mode below — this is
        // the raw "title bar has been held over the hinge" signal that backs the
        // explicit dwell gesture evaluated in OnDragEnded, which (unlike ordinary
        // auto-span) must still work when auto-span itself is disabled.
        UpdateGestureDwell(e.Hwnd, inHingeZone);

        var isEligible = topology is not null &&
            _snapIntegration.CanAutoSpan(settings.SnapIntegrationMode) &&
            DuoSnapPolicy.CanSpan(settings, e.ProcessName, isTopologyUnambiguous: true, dwellComplete: true) &&
            inHingeZone;

        if (!isEligible)
        {
            ClearCandidate();
            return;
        }

        if (_candidateHwnd != e.Hwnd)
        {
            ClearCandidate();
            _candidateHwnd = e.Hwnd;
            _candidateStartedAt = Stopwatch.GetTimestamp();
        }

        if (!_previewVisible && IsDwellComplete(settings))
        {
            _previewVisible = true;
            var target = HingeCalculator.ComputeSpanTarget(topology!.Hinge.DisplayA, topology.Hinge.DisplayB);
            SpanCandidateEntered?.Invoke(this, new SpanCandidateEventArgs(target));
        }
    }

    private void OnDragEnded(object? sender, WindowDragEventArgs e)
    {
        if (!_isStarted || e.Hwnd != _draggedHwnd) return;

        _draggedHwnd = IntPtr.Zero;
        var settings = _settings.Current.DuoSnap;
        var topology = _hinge.CurrentTopology;
        var isInHingeZone = topology is not null && topology.Hinge.Contains(e.CenterX, e.CenterY);
        var canSpan = _candidateHwnd == e.Hwnd && topology is not null &&
            _snapIntegration.CanAutoSpan(settings.SnapIntegrationMode) &&
            isInHingeZone &&
            DuoSnapPolicy.CanSpan(settings, e.ProcessName, isTopologyUnambiguous: true,
                dwellComplete: IsDwellComplete(settings));

        // A deliberate "grab the title bar and hold it over the hinge" gesture is a
        // confirmed request for Span, evaluated independently of AutoSpanEnabled/
        // CanAutoSpan (see UpdateGestureDwell) so it still works in configurations —
        // e.g. WindowsSnapDisabledManually, or AutoSpanEnabled turned off — where
        // ordinary drag-through auto-span is intentionally suppressed and every
        // layout must be an explicit user selection. It still MUST respect the
        // ignore list, though: a window the user has explicitly excluded from
        // DuoSnap (IgnoredExecutableNames or a profile's IsIgnored) must never be
        // spanned by any path, gesture included — reusing DuoSnapPolicy.IsIgnored
        // (the same check DuoSnapPolicy.CanSpan uses for auto-span above) rather
        // than inventing a separate ignore check for the gesture.
        var isTitleBarGestureConfirmed = topology is not null && isInHingeZone &&
            !DuoSnapPolicy.IsIgnored(settings, e.ProcessName) &&
            IsGestureDwellComplete(e.Hwnd, settings);

        ClearCandidate();
        ClearGesture();

        // A configured profile is a deliberate, explicit user choice for this
        // executable, so it takes priority over the ambient hinge-dwell auto-span
        // decision. This is invoked exactly once per drag completion (never from
        // OnDragMoved), satisfying "apply a profile at most once per completion".
        // Gated on the same hinge-zone containment check used for auto-span above
        // (isInHingeZone) so that dragging a profiled app anywhere on screen doesn't
        // forcibly relocate it — only a drag that ends near the hinge is eligible for
        // either auto-span or a profile-driven layout.
        var canApplyLayoutCommand = _snapIntegration.CanApplyLayoutCommand(settings.SnapIntegrationMode);
        if (canApplyLayoutCommand && isInHingeZone && (_profiles?.ApplyIfMatched(e.Hwnd, e.ProcessName) ?? false))
            return;

        if (canSpan)
        {
            var target = HingeCalculator.ComputeSpanTarget(topology!.Hinge.DisplayA, topology.Hinge.DisplayB);
            _span.Span(e.Hwnd, target);
            return;
        }

        if (canApplyLayoutCommand && isTitleBarGestureConfirmed)
        {
            var target = HingeCalculator.ComputeSpanTarget(topology!.Hinge.DisplayA, topology.Hinge.DisplayB);
            _span.Span(e.Hwnd, target);
            _logger.LogInformation("Title-bar hinge-hold gesture confirmed a span for window {Hwnd}", e.Hwnd);
            return;
        }

        if (canApplyLayoutCommand)
            _suggestions?.Evaluate(e.Hwnd, e.ProcessName);
    }

    private bool IsDwellComplete(DuoSnapSettings settings) =>
        _candidateHwnd != IntPtr.Zero &&
        Stopwatch.GetElapsedTime(_candidateStartedAt).TotalMilliseconds >= settings.DwellDurationMilliseconds;

    private void UpdateGestureDwell(IntPtr hwnd, bool inHingeZone)
    {
        if (!inHingeZone)
        {
            _gestureHwnd = IntPtr.Zero;
            _gestureEnteredAt = 0;
            return;
        }

        if (_gestureHwnd != hwnd)
        {
            _gestureHwnd = hwnd;
            _gestureEnteredAt = Stopwatch.GetTimestamp();
        }
    }

    private bool IsGestureDwellComplete(IntPtr hwnd, DuoSnapSettings settings) =>
        _gestureHwnd == hwnd && _gestureHwnd != IntPtr.Zero &&
        Stopwatch.GetElapsedTime(_gestureEnteredAt).TotalMilliseconds >=
            Math.Max(settings.DwellDurationMilliseconds, GestureMinimumHoldMilliseconds);

    private void ClearGesture()
    {
        _gestureHwnd = IntPtr.Zero;
        _gestureEnteredAt = 0;
    }

    private void ClearCandidate()
    {
        var previewWasVisible = _previewVisible;
        _candidateHwnd = IntPtr.Zero;
        _candidateStartedAt = 0;
        _previewVisible = false;
        if (previewWasVisible)
            SpanCandidateExited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}
