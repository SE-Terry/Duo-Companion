using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

// Resolves per-application layout profiles from settings and applies them via
// IWindowSpanService. Stateless by design: resolution always reads the current
// settings snapshot, so a profile edited/saved mid-session takes effect on the
// next completion without any extra invalidation. Callers (AutoSpanCoordinatorService)
// are responsible for invoking ApplyIfMatched only once per qualifying completion
// (a drag-end or a foreground change), never on every intermediate drag-move tick.
public sealed class AppLayoutProfileService : IAppLayoutProfileService
{
    private readonly ISettingsService _settings;
    private readonly IHingeTopologyService _hinge;
    private readonly IWindowSpanService _span;
    private readonly ILogger<AppLayoutProfileService> _logger;

    public AppLayoutProfileService(
        ISettingsService settings,
        IHingeTopologyService hinge,
        IWindowSpanService span,
        ILogger<AppLayoutProfileService> logger)
    {
        _settings = settings;
        _hinge = hinge;
        _span = span;
        _logger = logger;
    }

    public AppLayoutProfile? Resolve(string? executableName) =>
        _settings.Current.DuoSnap.Profiles.FirstOrDefault(profile => profile.MatchesExecutable(executableName));

    public bool ApplyIfMatched(IntPtr hwnd, string? executableName)
    {
        var profile = Resolve(executableName);
        if (profile is null || profile.IsIgnored) return false;

        var topology = _hinge.CurrentTopology;
        if (topology is null) return false;

        var target = WindowLayoutCalculator.Compute(topology, profile.Layout);
        _span.ApplyLayout(hwnd, target);
        _logger.LogInformation(
            "Applied profile layout {Layout} to window {Hwnd} for executable {Executable}",
            profile.Layout, hwnd, executableName);
        return true;
    }
}
