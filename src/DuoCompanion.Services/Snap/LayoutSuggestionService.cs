using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

// Deterministic, conservative layout suggestions for windows that don't already
// have an explicit AppLayoutProfile. Only a fixed set of browser/document/
// file-manager executables qualify; everything else (including unknown apps)
// gets no suggestion. Evaluate() only ever raises LayoutSuggested — it never
// moves a window. A suggestion is applied only once a caller explicitly
// confirms it via ApplySuggestedLayout, matching the "preview-only" design.
public sealed class LayoutSuggestionService : ILayoutSuggestionService
{
    private static readonly HashSet<string> SuggestableExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "msedge", "chrome", "firefox", "explorer", "acrord32", "sumatrapdf"
    };

    private readonly IAppLayoutProfileService _profiles;
    private readonly IHingeTopologyService _hinge;
    private readonly IWindowSpanService _span;
    private readonly ISettingsService _settings;
    private readonly ILogger<LayoutSuggestionService> _logger;
    private readonly Dictionary<IntPtr, WindowLayoutKind> _pendingSuggestions = new();

    public event EventHandler<LayoutSuggestedEventArgs>? LayoutSuggested;

    public LayoutSuggestionService(
        IAppLayoutProfileService profiles,
        IHingeTopologyService hinge,
        IWindowSpanService span,
        ISettingsService settings,
        ILogger<LayoutSuggestionService> logger)
    {
        _profiles = profiles;
        _hinge = hinge;
        _span = span;
        _settings = settings;
        _logger = logger;
    }

    public void Evaluate(IntPtr hwnd, string? executableName)
    {
        // A configured profile — whether it picks a layout or ignores the app —
        // always overrides a suggestion; the user has already made an explicit choice.
        if (_profiles.Resolve(executableName) is not null) return;
        // DuoSnapSettings.IgnoredExecutableNames is a separate, profile-free ignore
        // list; reuse the same DuoSnapPolicy.IsIgnored helper every other
        // ignore-list-respecting path (DuoSnapPolicy.CanSpan, the title-bar gesture
        // in AutoSpanCoordinatorService) already uses, so an executable ignored this
        // way never gets a suggestion either.
        if (DuoSnapPolicy.IsIgnored(_settings.Current.DuoSnap, executableName)) return;
        if (string.IsNullOrWhiteSpace(executableName) || !SuggestableExecutables.Contains(executableName)) return;

        const WindowLayoutKind layout = WindowLayoutKind.Span;
        _pendingSuggestions[hwnd] = layout;
        LayoutSuggested?.Invoke(this, new LayoutSuggestedEventArgs(hwnd, layout));
    }

    public void ApplySuggestedLayout(IntPtr hwnd)
    {
        if (!_pendingSuggestions.Remove(hwnd, out var layout)) return;

        var topology = _hinge.CurrentTopology;
        if (topology is null) return;

        var target = WindowLayoutCalculator.Compute(topology, layout);
        _span.ApplyLayout(hwnd, target);
        _logger.LogInformation("Applied confirmed suggested layout {Layout} to window {Hwnd}", layout, hwnd);
    }

    public void ForgetWindow(IntPtr hwnd) => _pendingSuggestions.Remove(hwnd);
}
