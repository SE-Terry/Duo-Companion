using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Snap;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class HingeTopologyServiceTests
{
    private static readonly DisplayInfo LeftDisplay =
        new(0, "LEFT", X: 0, Y: 0, Width: 1350, Height: 1800, IsPrimary: true);

    private static readonly DisplayInfo RightDisplay =
        new(1, "RIGHT", X: 1350, Y: 0, Width: 1350, Height: 1800, IsPrimary: false);

    [Fact]
    public void Start_computes_topology_using_the_configured_activation_half_width()
    {
        var fixture = CreateFixture(activationHalfWidth: 90);

        fixture.Service.Start();

        Assert.NotNull(fixture.Service.CurrentTopology);
        Assert.Equal(90, fixture.Service.CurrentTopology!.Hinge.ActivationHalfWidth);
    }

    [Fact]
    public void Settings_change_recomputes_topology_with_the_new_activation_half_width()
    {
        var fixture = CreateFixture(activationHalfWidth: 30);
        fixture.Service.Start();
        Assert.Equal(30, fixture.Service.CurrentTopology!.Hinge.ActivationHalfWidth);

        fixture.Settings.Current.DuoSnap.ActivationHalfWidth = 75;
        fixture.Settings.SettingsChanged += Raise.Event<EventHandler>(fixture.Settings, EventArgs.Empty);

        Assert.Equal(75, fixture.Service.CurrentTopology!.Hinge.ActivationHalfWidth);
    }

    [Fact]
    public void Settings_change_after_stop_does_not_recompute_topology()
    {
        var fixture = CreateFixture(activationHalfWidth: 30);
        fixture.Service.Start();
        fixture.Service.Stop();

        fixture.Settings.Current.DuoSnap.ActivationHalfWidth = 75;
        fixture.Settings.SettingsChanged += Raise.Event<EventHandler>(fixture.Settings, EventArgs.Empty);

        // Stop() unsubscribes from SettingsChanged, so the last-computed topology
        // (captured at Start(), with the original half-width) is left untouched.
        Assert.Equal(30, fixture.Service.CurrentTopology!.Hinge.ActivationHalfWidth);
    }

    private static Fixture CreateFixture(int activationHalfWidth)
    {
        var display = Substitute.For<IDisplayService>();
        display.GetAllDisplays().Returns(new[] { LeftDisplay, RightDisplay });

        var windowManager = Substitute.For<IWindowManagerService>();

        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            DuoSnap = new DuoSnapSettings { ActivationHalfWidth = activationHalfWidth }
        });

        var service = new HingeTopologyService(
            display, windowManager, settings, NullLogger<HingeTopologyService>.Instance);
        return new Fixture(service, settings);
    }

    private sealed record Fixture(HingeTopologyService Service, ISettingsService Settings);
}
