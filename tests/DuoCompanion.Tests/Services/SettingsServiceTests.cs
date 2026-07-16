using DuoCompanion.Core.Models;
using DuoCompanion.Services.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DuoCompanion.Tests.Services;

public sealed class AppSettingsTests
{
    [Fact]
    public void Default_theme_is_System()
    {
        var settings = new AppSettings();
        Assert.Equal("System", settings.Theme);
    }

    [Fact]
    public void Default_module_is_Keyboard()
    {
        var settings = new AppSettings();
        Assert.Equal("Keyboard", settings.DefaultModule);
    }

    [Fact]
    public void Default_opacity_is_1()
    {
        var settings = new AppSettings();
        Assert.Equal(1.0, settings.WindowOpacity);
    }

    [Fact]
    public void Settings_are_mutable()
    {
        var settings = new AppSettings();
        settings.Theme = "Dark";
        Assert.Equal("Dark", settings.Theme);
    }

    [Fact]
    public void Default_auto_hide_mode_is_Smart()
    {
        var settings = new AppSettings();
        Assert.Equal("Smart", settings.AutoHideMode);
    }

    [Fact]
    public void AutoHideMode_is_mutable()
    {
        var settings = new AppSettings();
        settings.AutoHideMode = "Always";
        Assert.Equal("Always", settings.AutoHideMode);
    }

    [Fact]
    public void Default_duo_snap_auto_span_enabled_is_true()
    {
        var settings = new AppSettings();
        Assert.True(settings.DuoSnap.AutoSpanEnabled);
    }

    [Fact]
    public void Duo_snap_auto_span_enabled_is_mutable()
    {
        var settings = new AppSettings();
        settings.DuoSnap.AutoSpanEnabled = false;
        Assert.False(settings.DuoSnap.AutoSpanEnabled);
    }

    [Fact]
    public void Default_duo_snap_activation_half_width_matches_the_specified_default()
    {
        var settings = new AppSettings();
        Assert.Equal(DuoSnapSettings.DefaultActivationHalfWidth, settings.DuoSnap.ActivationHalfWidth);
    }

    [Fact]
    public void Default_duo_snap_overlay_opacity_matches_the_specified_default()
    {
        var settings = new AppSettings();
        Assert.Equal(DuoSnapSettings.DefaultOverlayOpacity, settings.DuoSnap.OverlayOpacity);
    }

    [Fact]
    public void Default_duo_snap_fade_duration_matches_the_specified_default()
    {
        var settings = new AppSettings();
        Assert.Equal(DuoSnapSettings.DefaultFadeDurationMilliseconds, settings.DuoSnap.FadeDurationMilliseconds);
    }

    [Fact]
    public void Default_duo_snap_dwell_duration_matches_the_specified_default()
    {
        var settings = new AppSettings();
        Assert.Equal(DuoSnapSettings.DefaultDwellDurationMilliseconds, settings.DuoSnap.DwellDurationMilliseconds);
    }

    [Fact]
    public void Default_duo_snap_restore_behavior_is_OnNextDrag()
    {
        var settings = new AppSettings();
        Assert.Equal(RestoreBehavior.OnNextDrag, settings.DuoSnap.RestoreBehavior);
    }

    [Fact]
    public void Default_duo_snap_integration_mode_is_ExtendWindowsSnap()
    {
        var settings = new AppSettings();
        Assert.Equal(SnapIntegrationMode.ExtendWindowsSnap, settings.DuoSnap.SnapIntegrationMode);
    }

    [Fact]
    public void Default_duo_snap_ignored_executable_names_is_empty()
    {
        var settings = new AppSettings();
        Assert.Empty(settings.DuoSnap.IgnoredExecutableNames);
    }

    [Fact]
    public void Default_duo_snap_profiles_is_empty()
    {
        var settings = new AppSettings();
        Assert.Empty(settings.DuoSnap.Profiles);
    }

    [Fact]
    public void Default_duo_snap_hotkey_bindings_is_empty()
    {
        var settings = new AppSettings();
        Assert.Empty(settings.DuoSnap.HotkeyBindings);
    }
}

public sealed class SettingsServiceTests
{
    [Fact]
    public void Save_raises_settings_changed()
    {
        // Isolated temp settings path (matching the sibling tests below) rather
        // than the public logger-only constructor, which resolves the real
        // %LOCALAPPDATA%\DuoCompanion\settings.json path — Save() must never
        // write to a real user's settings file during a test run.
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var settingsPath = Path.Combine(dir, "settings.json");

        try
        {
            var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
            var changes = 0;
            service.SettingsChanged += (_, _) => changes++;

            service.Save();

            Assert.Equal(1, changes);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Save_does_not_raise_settings_changed_when_persistence_fails()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsPath);

        try
        {
            var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
            var changes = 0;
            service.SettingsChanged += (_, _) => changes++;

            service.Save();

            Assert.Equal(0, changes);
        }
        finally
        {
            Directory.Delete(settingsPath);
        }
    }

    [Fact]
    public void Load_of_settings_json_predating_duosnap_populates_duosnap_defaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var settingsPath = Path.Combine(dir, "settings.json");
        File.WriteAllText(settingsPath, "{\"Theme\":\"Dark\",\"DefaultModule\":\"Clipboard\"}");

        try
        {
            var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);

            Assert.Equal("Dark", service.Current.Theme);
            Assert.Equal("Clipboard", service.Current.DefaultModule);
            Assert.Equal(DuoSnapSettings.DefaultActivationHalfWidth, service.Current.DuoSnap.ActivationHalfWidth);
            Assert.Equal(DuoSnapSettings.DefaultOverlayOpacity, service.Current.DuoSnap.OverlayOpacity);
            Assert.Equal(DuoSnapSettings.DefaultFadeDurationMilliseconds, service.Current.DuoSnap.FadeDurationMilliseconds);
            Assert.Equal(DuoSnapSettings.DefaultDwellDurationMilliseconds, service.Current.DuoSnap.DwellDurationMilliseconds);
            Assert.Equal(RestoreBehavior.OnNextDrag, service.Current.DuoSnap.RestoreBehavior);
            Assert.Equal(SnapIntegrationMode.ExtendWindowsSnap, service.Current.DuoSnap.SnapIntegrationMode);
            Assert.Empty(service.Current.DuoSnap.IgnoredExecutableNames);
            Assert.Empty(service.Current.DuoSnap.Profiles);
            Assert.Empty(service.Current.DuoSnap.HotkeyBindings);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_of_settings_json_with_partial_duosnap_object_retains_specified_field_and_fills_the_rest_with_defaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var settingsPath = Path.Combine(dir, "settings.json");
        File.WriteAllText(settingsPath, "{\"DuoSnap\":{\"AutoSpanEnabled\":false}}");

        try
        {
            var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);

            Assert.False(service.Current.DuoSnap.AutoSpanEnabled);
            Assert.Equal(DuoSnapSettings.DefaultActivationHalfWidth, service.Current.DuoSnap.ActivationHalfWidth);
            Assert.Equal(DuoSnapSettings.DefaultOverlayOpacity, service.Current.DuoSnap.OverlayOpacity);
            Assert.Equal(DuoSnapSettings.DefaultFadeDurationMilliseconds, service.Current.DuoSnap.FadeDurationMilliseconds);
            Assert.Equal(DuoSnapSettings.DefaultDwellDurationMilliseconds, service.Current.DuoSnap.DwellDurationMilliseconds);
            Assert.Equal(RestoreBehavior.OnNextDrag, service.Current.DuoSnap.RestoreBehavior);
            Assert.Equal(SnapIntegrationMode.ExtendWindowsSnap, service.Current.DuoSnap.SnapIntegrationMode);
            Assert.Empty(service.Current.DuoSnap.HotkeyBindings);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
