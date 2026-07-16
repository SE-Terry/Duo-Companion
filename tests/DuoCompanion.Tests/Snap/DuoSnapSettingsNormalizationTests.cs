using DuoCompanion.Core.Models;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class DuoSnapSettingsNormalizationTests
{
    [Fact]
    public void Normalize_retains_the_default_values()
    {
        var settings = new DuoSnapSettings();

        settings.Normalize();

        Assert.Equal(DuoSnapSettings.DefaultActivationHalfWidth, settings.ActivationHalfWidth);
        Assert.Equal(DuoSnapSettings.DefaultOverlayOpacity, settings.OverlayOpacity);
        Assert.Equal(DuoSnapSettings.DefaultFadeDurationMilliseconds, settings.FadeDurationMilliseconds);
        Assert.Equal(DuoSnapSettings.DefaultDwellDurationMilliseconds, settings.DwellDurationMilliseconds);
        Assert.True(settings.AutoSpanEnabled);
    }

    [Fact]
    public void Normalize_clamps_invalid_persisted_values()
    {
        var settings = new DuoSnapSettings
        {
            ActivationHalfWidth = 0,
            OverlayOpacity = 2,
            FadeDurationMilliseconds = 2000,
            DwellDurationMilliseconds = -1
        };

        settings.Normalize();

        Assert.Equal(5, settings.ActivationHalfWidth);
        Assert.Equal(.80, settings.OverlayOpacity);
        Assert.Equal(1000, settings.FadeDurationMilliseconds);
        Assert.Equal(0, settings.DwellDurationMilliseconds);
    }

    [Fact]
    public void Normalize_drops_blank_hotkey_bindings_but_keeps_assigned_ones()
    {
        var settings = new DuoSnapSettings
        {
            HotkeyBindings = new Dictionary<WindowLayoutKind, string>
            {
                [WindowLayoutKind.Span] = "Ctrl+Alt+S",
                [WindowLayoutKind.Left] = "",
                [WindowLayoutKind.Right] = "   ",
            }
        };

        settings.Normalize();

        Assert.Equal(new Dictionary<WindowLayoutKind, string> { [WindowLayoutKind.Span] = "Ctrl+Alt+S" },
            settings.HotkeyBindings);
    }
}
