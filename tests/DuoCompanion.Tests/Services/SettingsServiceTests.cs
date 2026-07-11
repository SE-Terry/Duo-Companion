using DuoCompanion.Core.Models;
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
}
