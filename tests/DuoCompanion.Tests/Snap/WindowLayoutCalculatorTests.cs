using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class WindowLayoutCalculatorTests
{
    private static readonly DuoDisplayTopology VerticalTopology = new(
        new HingeZone(
            new DisplayInfo(0, "LEFT", 0, 0, 1350, 1800, true),
            new DisplayInfo(1, "RIGHT", 1350, 0, 1350, 1800, false),
            IsVertical: true,
            ActivationCenter: 1350,
            ActivationHalfWidth: 30),
        HasExternalDisplays: false);

    private static readonly DuoDisplayTopology HorizontalTopology = new(
        new HingeZone(
            new DisplayInfo(0, "TOP", 0, 0, 1800, 1350, true),
            new DisplayInfo(1, "BOTTOM", 0, 1350, 1800, 1350, false),
            IsVertical: false,
            ActivationCenter: 1350,
            ActivationHalfWidth: 30),
        HasExternalDisplays: false);

    [Fact]
    public void Compute_span_returns_the_full_vertical_pair_bounds()
    {
        var target = WindowLayoutCalculator.Compute(VerticalTopology, WindowLayoutKind.Span);

        Assert.Equal(new WindowLayoutTarget(0, 0, 2700, 1800), target);
    }

    [Theory]
    [InlineData(WindowLayoutKind.Left, 0, 0, 1350, 1800)]
    [InlineData(WindowLayoutKind.Right, 1350, 0, 1350, 1800)]
    public void Compute_panel_layout_returns_the_requested_vertical_panel(
        WindowLayoutKind layout, int left, int top, int width, int height)
    {
        var target = WindowLayoutCalculator.Compute(VerticalTopology, layout);

        Assert.Equal(new WindowLayoutTarget(left, top, width, height), target);
    }

    [Fact]
    public void Compute_70_30_assigns_seventy_percent_to_left_panel()
    {
        var target = WindowLayoutCalculator.Compute(VerticalTopology, WindowLayoutKind.Left70Right30);

        Assert.Equal(new WindowLayoutTarget(0, 0, 1890, 1800), target);
    }

    [Fact]
    public void Compute_30_70_assigns_thirty_percent_to_left_panel()
    {
        var target = WindowLayoutCalculator.Compute(VerticalTopology, WindowLayoutKind.Left30Right70);

        Assert.Equal(new WindowLayoutTarget(0, 0, 810, 1800), target);
    }

    [Theory]
    [InlineData(WindowLayoutKind.Span, 0, 0, 1800, 2700)]
    [InlineData(WindowLayoutKind.Left, 0, 0, 1800, 1350)]
    [InlineData(WindowLayoutKind.Right, 0, 1350, 1800, 1350)]
    [InlineData(WindowLayoutKind.Left70Right30, 0, 0, 1800, 1890)]
    [InlineData(WindowLayoutKind.Left30Right70, 0, 0, 1800, 810)]
    public void Compute_uses_the_vertical_axis_for_horizontal_topologies(
        WindowLayoutKind layout, int left, int top, int width, int height)
    {
        var target = WindowLayoutCalculator.Compute(HorizontalTopology, layout);

        Assert.Equal(new WindowLayoutTarget(left, top, width, height), target);
    }
}
