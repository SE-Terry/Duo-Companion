using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Snap;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class LayoutSuggestionServiceTests
{
    private static readonly IntPtr Window = new(9);
    private static readonly DuoDisplayTopology ValidTopology = new(
        new HingeZone(
            new DisplayInfo(0, "LEFT", 0, 0, 1350, 1800, true),
            new DisplayInfo(1, "RIGHT", 1350, 0, 1350, 1800, false),
            IsVertical: true,
            ActivationCenter: 1350,
            ActivationHalfWidth: 30),
        HasExternalDisplays: false);

    [Theory]
    [InlineData("msedge")]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("explorer")]
    [InlineData("acrord32")]
    [InlineData("sumatrapdf")]
    public void Evaluate_suggests_span_for_browser_document_and_file_manager_executables(string executableName)
    {
        var fixture = CreateFixture();
        WindowLayoutKind? suggested = null;
        fixture.Service.LayoutSuggested += (_, e) => suggested = e.Layout;

        fixture.Service.Evaluate(Window, executableName.ToUpperInvariant());

        Assert.Equal(WindowLayoutKind.Span, suggested);
    }

    [Fact]
    public void Evaluate_does_not_suggest_for_an_unknown_executable()
    {
        var fixture = CreateFixture();
        var raised = false;
        fixture.Service.LayoutSuggested += (_, _) => raised = true;

        fixture.Service.Evaluate(Window, "notepad");

        Assert.False(raised);
    }

    [Fact]
    public void Evaluate_does_not_suggest_when_an_explicit_profile_already_matches()
    {
        var fixture = CreateFixture(profile: new AppLayoutProfile { ExecutableName = "chrome", Layout = WindowLayoutKind.Left });
        var raised = false;
        fixture.Service.LayoutSuggested += (_, _) => raised = true;

        fixture.Service.Evaluate(Window, "chrome");

        Assert.False(raised);
    }

    [Fact]
    public void Evaluate_does_not_suggest_when_the_matching_profile_is_ignored()
    {
        var fixture = CreateFixture(profile: new AppLayoutProfile { ExecutableName = "chrome", IsIgnored = true });
        var raised = false;
        fixture.Service.LayoutSuggested += (_, _) => raised = true;

        fixture.Service.Evaluate(Window, "chrome");

        Assert.False(raised);
    }

    [Fact]
    public void Evaluate_does_not_suggest_when_the_executable_is_on_the_plain_ignore_list()
    {
        // Distinct from Evaluate_does_not_suggest_when_the_matching_profile_is_ignored:
        // no AppLayoutProfile is configured at all here — this is
        // DuoSnapSettings.IgnoredExecutableNames, the separate free-text ignore
        // list that has no profile behind it.
        var fixture = CreateFixture(ignoredExecutableNames: ["chrome"]);
        var raised = false;
        fixture.Service.LayoutSuggested += (_, _) => raised = true;

        fixture.Service.Evaluate(Window, "chrome");

        Assert.False(raised);
    }

    [Fact]
    public void Evaluate_never_applies_a_layout_directly()
    {
        var fixture = CreateFixture();

        fixture.Service.Evaluate(Window, "chrome");

        fixture.Span.DidNotReceive().ApplyLayout(Arg.Any<IntPtr>(), Arg.Any<WindowLayoutTarget>());
    }

    [Fact]
    public void ApplySuggestedLayout_applies_the_suggested_layout_after_confirmation()
    {
        var fixture = CreateFixture();
        fixture.Service.Evaluate(Window, "chrome");

        fixture.Service.ApplySuggestedLayout(Window);

        fixture.Span.Received(1).ApplyLayout(Window, new WindowLayoutTarget(0, 0, 2700, 1800));
    }

    [Fact]
    public void ApplySuggestedLayout_does_nothing_without_a_prior_suggestion()
    {
        var fixture = CreateFixture();

        fixture.Service.ApplySuggestedLayout(Window);

        fixture.Span.DidNotReceive().ApplyLayout(Arg.Any<IntPtr>(), Arg.Any<WindowLayoutTarget>());
    }

    private static Fixture CreateFixture(AppLayoutProfile? profile = null, List<string>? ignoredExecutableNames = null)
    {
        var profiles = Substitute.For<IAppLayoutProfileService>();
        profiles.Resolve(Arg.Any<string?>()).Returns((AppLayoutProfile?)null);
        if (profile is not null)
            profiles.Resolve(profile.ExecutableName).Returns(profile);

        var hinge = Substitute.For<IHingeTopologyService>();
        hinge.CurrentTopology.Returns(ValidTopology);

        var span = Substitute.For<IWindowSpanService>();

        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            DuoSnap = new DuoSnapSettings { IgnoredExecutableNames = ignoredExecutableNames ?? [] }
        });

        var service = new LayoutSuggestionService(
            profiles, hinge, span, settings, NullLogger<LayoutSuggestionService>.Instance);
        return new Fixture(service, span);
    }

    private sealed record Fixture(LayoutSuggestionService Service, IWindowSpanService Span);
}
