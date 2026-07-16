using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class DuoSnapPolicyTests
{
    [Fact]
    public void CanSpan_returns_false_when_auto_span_is_disabled()
    {
        var settings = new DuoSnapSettings { IsEnabled = false };

        Assert.False(DuoSnapPolicy.CanSpan(settings, "notepad.exe", isTopologyUnambiguous: true, dwellComplete: true));
    }

    [Fact]
    public void CanSpan_returns_false_for_an_ignored_executable()
    {
        var settings = new DuoSnapSettings { IgnoredExecutableNames = ["notepad.exe"] };

        Assert.False(DuoSnapPolicy.CanSpan(settings, "NOTEPAD.EXE", isTopologyUnambiguous: true, dwellComplete: true));
    }

    [Fact]
    public void CanSpan_returns_false_for_an_ambiguous_topology()
    {
        Assert.False(DuoSnapPolicy.CanSpan(new DuoSnapSettings(), "notepad.exe", isTopologyUnambiguous: false, dwellComplete: true));
    }

    [Fact]
    public void CanSpan_returns_false_until_the_dwell_is_complete()
    {
        Assert.False(DuoSnapPolicy.CanSpan(new DuoSnapSettings(), "notepad.exe", isTopologyUnambiguous: true, dwellComplete: false));
    }

    [Fact]
    public void AppLayoutProfile_matches_executable_names_case_insensitively()
    {
        var profile = new AppLayoutProfile { ExecutableName = "notepad.exe" };

        Assert.True(profile.MatchesExecutable("NOTEPAD.EXE"));
    }

    [Fact]
    public void Normalize_clamps_persisted_numeric_values_and_restores_invalid_enums()
    {
        var settings = new DuoSnapSettings
        {
            ActivationHalfWidth = 500,
            OverlayOpacity = 1.5,
            FadeDurationMilliseconds = -1,
            DwellDurationMilliseconds = 2000,
            RestoreBehavior = (RestoreBehavior)99,
            SnapIntegrationMode = (SnapIntegrationMode)99,
            IgnoredExecutableNames = null!,
            Profiles = null!
        };

        settings.Normalize();

        Assert.Equal(120, settings.ActivationHalfWidth);
        Assert.Equal(.80, settings.OverlayOpacity);
        Assert.Equal(0, settings.FadeDurationMilliseconds);
        Assert.Equal(1500, settings.DwellDurationMilliseconds);
        Assert.Equal(RestoreBehavior.OnNextDrag, settings.RestoreBehavior);
        Assert.Equal(SnapIntegrationMode.ExtendWindowsSnap, settings.SnapIntegrationMode);
        Assert.Empty(settings.IgnoredExecutableNames);
        Assert.Empty(settings.Profiles);
    }
}
