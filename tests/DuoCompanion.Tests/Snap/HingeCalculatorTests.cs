using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class HingeCalculatorTests
{
    private static readonly DisplayInfo LeftDisplay =
        new(0, "LEFT", X: 0, Y: 0, Width: 1350, Height: 1800, IsPrimary: true);

    private static readonly DisplayInfo RightDisplay =
        new(1, "RIGHT", X: 1350, Y: 0, Width: 1350, Height: 1800, IsPrimary: false);

    private static readonly DisplayInfo TopDisplay =
        new(0, "TOP", X: 0, Y: 0, Width: 1800, Height: 1350, IsPrimary: true);

    private static readonly DisplayInfo BottomDisplay =
        new(1, "BOTTOM", X: 0, Y: 1350, Width: 1800, Height: 1350, IsPrimary: false);

    private static readonly DisplayInfo DetachedExternalDisplay =
        new(2, "EXTERNAL", X: 4000, Y: 0, Width: 1920, Height: 1080, IsPrimary: false);

    private static readonly DisplayInfo ExternalRightDisplay =
        new(2, "EXTERNAL_RIGHT", X: 2700, Y: 0, Width: 1350, Height: 1800, IsPrimary: false);

    [Fact]
    public void ComputeHingeZone_returns_null_for_single_display()
    {
        var result = HingeCalculator.ComputeHingeZone(new[] { LeftDisplay });
        Assert.Null(result);
    }

    [Fact]
    public void ComputeHingeZone_detects_vertical_hinge_for_side_by_side_displays()
    {
        var zone = HingeCalculator.ComputeHingeZone(new[] { LeftDisplay, RightDisplay });

        Assert.NotNull(zone);
        Assert.True(zone!.IsVertical);
        Assert.Equal(1350, zone.ActivationCenter);
        Assert.Equal(30, zone.ActivationHalfWidth);
    }

    [Fact]
    public void ComputeHingeZone_detects_horizontal_hinge_for_stacked_displays()
    {
        var zone = HingeCalculator.ComputeHingeZone(new[] { TopDisplay, BottomDisplay });

        Assert.NotNull(zone);
        Assert.False(zone!.IsVertical);
        Assert.Equal(1350, zone.ActivationCenter);
    }

    [Fact]
    public void ComputeHingeZone_accepts_displays_in_either_order()
    {
        var zone = HingeCalculator.ComputeHingeZone(new[] { RightDisplay, LeftDisplay });

        Assert.NotNull(zone);
        Assert.Equal(1350, zone.ActivationCenter);
    }

    [Fact]
    public void ComputeDuoTopology_returns_vertical_pair_with_detached_external_display()
    {
        var topology = HingeCalculator.ComputeDuoTopology(
            new[] { LeftDisplay, RightDisplay, DetachedExternalDisplay });

        Assert.NotNull(topology);
        Assert.True(topology!.Hinge.IsVertical);
        Assert.Equal(1350, topology.Hinge.ActivationCenter);
        Assert.True(topology.HasExternalDisplays);
    }

    [Fact]
    public void ComputeDuoTopology_uses_a_non_default_activation_half_width_when_supplied()
    {
        var topology = HingeCalculator.ComputeDuoTopology(
            new[] { LeftDisplay, RightDisplay }, activationHalfWidth: 90);

        Assert.NotNull(topology);
        Assert.Equal(90, topology!.Hinge.ActivationHalfWidth);
    }

    [Fact]
    public void ComputeDuoTopology_returns_null_when_two_pairs_are_valid()
    {
        var topology = HingeCalculator.ComputeDuoTopology(
            new[] { LeftDisplay, RightDisplay, ExternalRightDisplay });

        Assert.Null(topology);
    }

    [Fact]
    public void ComputeDuoTopology_returns_null_for_offset_or_non_touching_displays()
    {
        var offsetRightDisplay = RightDisplay with { X = 1400, Y = 25 };

        var topology = HingeCalculator.ComputeDuoTopology(new[] { LeftDisplay, offsetRightDisplay });

        Assert.Null(topology);
    }

    [Fact]
    public void ComputeDuoTopology_returns_stacked_pair()
    {
        var topology = HingeCalculator.ComputeDuoTopology(new[] { TopDisplay, BottomDisplay });

        Assert.NotNull(topology);
        Assert.False(topology!.Hinge.IsVertical);
        Assert.Equal(1350, topology.Hinge.ActivationCenter);
        Assert.False(topology.HasExternalDisplays);
    }

    [Fact]
    public void HingeZone_Contains_is_true_inside_vertical_activation_band()
    {
        var zone = new HingeZone(LeftDisplay, RightDisplay, IsVertical: true, ActivationCenter: 1350, ActivationHalfWidth: 30);

        Assert.True(zone.Contains(1350, 900));
        Assert.True(zone.Contains(1325, 900));
        Assert.True(zone.Contains(1380, 900));
        Assert.False(zone.Contains(1300, 900));
        Assert.False(zone.Contains(1400, 900));
    }

    [Fact]
    public void HingeZone_Contains_is_true_inside_horizontal_activation_band()
    {
        var zone = new HingeZone(TopDisplay, BottomDisplay, IsVertical: false, ActivationCenter: 1350, ActivationHalfWidth: 30);

        Assert.True(zone.Contains(900, 1350));
        Assert.False(zone.Contains(900, 1200));
    }

    [Fact]
    public void ComputeSpanTarget_unions_both_displays_side_by_side()
    {
        var target = HingeCalculator.ComputeSpanTarget(LeftDisplay, RightDisplay);

        Assert.Equal(0, target.Left);
        Assert.Equal(0, target.Top);
        Assert.Equal(2700, target.Width);
        Assert.Equal(1800, target.Height);
    }

    [Fact]
    public void ComputeSpanTarget_unions_both_displays_stacked()
    {
        var target = HingeCalculator.ComputeSpanTarget(TopDisplay, BottomDisplay);

        Assert.Equal(0, target.Left);
        Assert.Equal(0, target.Top);
        Assert.Equal(1800, target.Width);
        Assert.Equal(2700, target.Height);
    }

    [Fact]
    public void ComputeSpanTarget_is_order_independent()
    {
        var a = HingeCalculator.ComputeSpanTarget(LeftDisplay, RightDisplay);
        var b = HingeCalculator.ComputeSpanTarget(RightDisplay, LeftDisplay);

        Assert.Equal(a, b);
    }
}
