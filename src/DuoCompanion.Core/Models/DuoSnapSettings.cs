using System.Text.Json.Serialization;

namespace DuoCompanion.Core.Models;

public enum SnapIntegrationMode
{
    ExtendWindowsSnap,
    ReplaceWindowsSnap,
    WindowsSnapDisabledManually
}

public enum RestoreBehavior
{
    OnNextDrag,
    Never
}

public sealed class DuoSnapSettings
{
    public const int DefaultActivationHalfWidth = 30;
    public const double DefaultOverlayOpacity = .25;
    public const int DefaultFadeDurationMilliseconds = 150;
    public const int DefaultDwellDurationMilliseconds = 0;

    public bool AutoSpanEnabled { get; set; } = true;
    public int ActivationHalfWidth { get; set; } = DefaultActivationHalfWidth;
    public double OverlayOpacity { get; set; } = DefaultOverlayOpacity;
    public int FadeDurationMilliseconds { get; set; } = DefaultFadeDurationMilliseconds;
    public int DwellDurationMilliseconds { get; set; } = DefaultDwellDurationMilliseconds;
    public RestoreBehavior RestoreBehavior { get; set; } = RestoreBehavior.OnNextDrag;
    public SnapIntegrationMode SnapIntegrationMode { get; set; } = SnapIntegrationMode.ExtendWindowsSnap;
    public List<string> IgnoredExecutableNames { get; set; } = [];
    public List<AppLayoutProfile> Profiles { get; set; } = [];

    // Keyed by the layout it invokes (Left/Right/Span/Left70Right30/Left30Right70).
    // A missing or blank entry means that action has no assigned hotkey — hotkeys
    // are disabled until the user explicitly assigns one. Raw text is the same
    // grammar accepted by HotkeyBinding.Parse (e.g. "Ctrl+Alt+S").
    public Dictionary<WindowLayoutKind, string> HotkeyBindings { get; set; } = new();

    [JsonIgnore]
    public bool IsEnabled
    {
        get => AutoSpanEnabled;
        set => AutoSpanEnabled = value;
    }

    public DuoSnapSettings Normalize()
    {
        ActivationHalfWidth = Math.Clamp(ActivationHalfWidth, 5, 120);
        OverlayOpacity = Math.Clamp(OverlayOpacity, .05, .80);
        FadeDurationMilliseconds = Math.Clamp(FadeDurationMilliseconds, 0, 1000);
        DwellDurationMilliseconds = Math.Clamp(DwellDurationMilliseconds, 0, 1500);

        if (!Enum.IsDefined<RestoreBehavior>(this.RestoreBehavior))
            this.RestoreBehavior = global::DuoCompanion.Core.Models.RestoreBehavior.OnNextDrag;
        if (!Enum.IsDefined<SnapIntegrationMode>(this.SnapIntegrationMode))
            this.SnapIntegrationMode = global::DuoCompanion.Core.Models.SnapIntegrationMode.ExtendWindowsSnap;

        IgnoredExecutableNames ??= [];
        Profiles ??= [];

        HotkeyBindings ??= new();
        foreach (var action in HotkeyBindings
                     .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                     .Select(kv => kv.Key)
                     .ToList())
        {
            HotkeyBindings.Remove(action);
        }

        return this;
    }
}
