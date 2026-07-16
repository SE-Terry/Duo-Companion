using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Snap;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class AppLayoutProfileServiceTests
{
    private static readonly IntPtr Window = new(7);
    private static readonly DuoDisplayTopology ValidTopology = new(
        new HingeZone(
            new DisplayInfo(0, "LEFT", 0, 0, 1350, 1800, true),
            new DisplayInfo(1, "RIGHT", 1350, 0, 1350, 1800, false),
            IsVertical: true,
            ActivationCenter: 1350,
            ActivationHalfWidth: 30),
        HasExternalDisplays: false);

    [Fact]
    public void Resolve_matches_the_configured_executable_name_case_insensitively()
    {
        var fixture = CreateFixture(new AppLayoutProfile { ExecutableName = "notepad", Layout = WindowLayoutKind.Left });

        var profile = fixture.Service.Resolve("NOTEPAD");

        Assert.NotNull(profile);
        Assert.Equal(WindowLayoutKind.Left, profile!.Layout);
    }

    [Fact]
    public void Resolve_returns_null_for_an_unconfigured_executable()
    {
        var fixture = CreateFixture(new AppLayoutProfile { ExecutableName = "notepad", Layout = WindowLayoutKind.Left });

        Assert.Null(fixture.Service.Resolve("chrome"));
    }

    [Fact]
    public void ApplyIfMatched_applies_the_configured_layout_for_a_matching_profile()
    {
        var fixture = CreateFixture(new AppLayoutProfile { ExecutableName = "notepad", Layout = WindowLayoutKind.Left });

        var applied = fixture.Service.ApplyIfMatched(Window, "notepad");

        Assert.True(applied);
        fixture.Span.Received(1).ApplyLayout(Window, new WindowLayoutTarget(0, 0, 1350, 1800));
    }

    [Fact]
    public void ApplyIfMatched_does_not_apply_an_ignored_profile()
    {
        var fixture = CreateFixture(new AppLayoutProfile { ExecutableName = "notepad", IsIgnored = true });

        var applied = fixture.Service.ApplyIfMatched(Window, "notepad");

        Assert.False(applied);
        fixture.Span.DidNotReceive().ApplyLayout(Arg.Any<IntPtr>(), Arg.Any<WindowLayoutTarget>());
    }

    [Fact]
    public void ApplyIfMatched_does_nothing_when_no_profile_matches()
    {
        var fixture = CreateFixture(new AppLayoutProfile { ExecutableName = "notepad", Layout = WindowLayoutKind.Left });

        var applied = fixture.Service.ApplyIfMatched(Window, "chrome");

        Assert.False(applied);
        fixture.Span.DidNotReceive().ApplyLayout(Arg.Any<IntPtr>(), Arg.Any<WindowLayoutTarget>());
    }

    [Fact]
    public void ApplyIfMatched_does_nothing_when_the_hinge_topology_is_ambiguous()
    {
        var fixture = CreateFixture(new AppLayoutProfile { ExecutableName = "notepad", Layout = WindowLayoutKind.Left }, hasTopology: false);

        var applied = fixture.Service.ApplyIfMatched(Window, "notepad");

        Assert.False(applied);
        fixture.Span.DidNotReceive().ApplyLayout(Arg.Any<IntPtr>(), Arg.Any<WindowLayoutTarget>());
    }

    private static Fixture CreateFixture(AppLayoutProfile profile, bool hasTopology = true)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            DuoSnap = new DuoSnapSettings { Profiles = [profile] }
        });

        var hinge = Substitute.For<IHingeTopologyService>();
        hinge.CurrentTopology.Returns(hasTopology ? ValidTopology : null);

        var span = Substitute.For<IWindowSpanService>();

        var service = new AppLayoutProfileService(settings, hinge, span, NullLogger<AppLayoutProfileService>.Instance);
        return new Fixture(service, span);
    }

    private sealed record Fixture(AppLayoutProfileService Service, IWindowSpanService Span);
}
