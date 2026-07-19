using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

public sealed class HingeTopologyService : IHingeTopologyService, IDisposable
{
    private readonly IDisplayService _display;
    private readonly IWindowManagerService _windowManager;
    private readonly ISettingsService _settings;
    private readonly ILogger<HingeTopologyService> _logger;

    private bool _isStarted;

    public DuoDisplayTopology? CurrentTopology { get; private set; }
    public event EventHandler? TopologyChanged;

    // ISettingsService (not IDuoSnapSettingsMonitor) to match the pattern already
    // used by sibling Snap services (AutoSpanCoordinatorService, AppLayoutProfileService,
    // LayoutSuggestionService) — they all read settings.Current.DuoSnap live and
    // subscribe to ISettingsService.SettingsChanged directly rather than going through
    // the monitor wrapper, which today is only consumed by the App/UI layer.
    public HingeTopologyService(
        IDisplayService display, IWindowManagerService windowManager, ISettingsService settings,
        ILogger<HingeTopologyService> logger)
    {
        _display = display;
        _windowManager = windowManager;
        _settings = settings;
        _logger = logger;
    }

    public void Start()
    {
        if (_isStarted) return;
        _isStarted = true;
        Recompute();
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public void Stop()
    {
        if (!_isStarted) return;
        _isStarted = false;
        _windowManager.DisplayConfigurationChanged -= OnDisplayConfigurationChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
    }

    private void OnDisplayConfigurationChanged(object? sender, EventArgs e) => Recompute();

    // ActivationHalfWidth can change live via the Settings page (clamped 5-120px),
    // so a settings save must recompute the hinge zone with the new width — not
    // just re-read it lazily on the next display-configuration change.
    private void OnSettingsChanged(object? sender, EventArgs e) => Recompute();

    private void Recompute()
    {
        var activationHalfWidth = _settings.Current.DuoSnap.ActivationHalfWidth;
        var displays = _display.GetAllDisplays();
        CurrentTopology = HingeCalculator.ComputeDuoTopology(displays, activationHalfWidth);
        _logger.LogInformation("Duo topology recomputed: {Topology}", CurrentTopology);
        WriteHingeDebugLog(displays, activationHalfWidth);
        TopologyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();

    // Temporary diagnostic for the "hinge snap does nothing" investigation —
    // dumps the raw display geometry Windows reports alongside the pairing
    // result, since HingeCalculator requires byte-exact edge alignment and a
    // real monitor arrangement is a much likelier source of a mismatch than
    // anything reachable by reading the code. Remove once root-caused.
    private static void WriteHingeDebugLog(IReadOnlyList<DisplayInfo> displays, int activationHalfWidth)
    {
        try
        {
            var lines = new List<string>
            {
                $"{DateTime.Now:O} activationHalfWidth={activationHalfWidth} displayCount={displays.Count}"
            };
            foreach (var d in displays)
                lines.Add($"  {d}");

            System.IO.File.AppendAllLines(
                System.IO.Path.Combine(AppContext.BaseDirectory, "hinge-debug.log"), lines);
        }
        catch
        {
            // Diagnostics must not affect app behavior.
        }
    }
}
