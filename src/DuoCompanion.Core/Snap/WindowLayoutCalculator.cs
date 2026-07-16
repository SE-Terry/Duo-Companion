using DuoCompanion.Core.Models;

namespace DuoCompanion.Core.Snap;

public static class WindowLayoutCalculator
{
    public static WindowLayoutTarget Compute(DuoDisplayTopology topology, WindowLayoutKind layout)
    {
        ArgumentNullException.ThrowIfNull(topology);

        var first = topology.Hinge.DisplayA;
        var second = topology.Hinge.DisplayB;
        var span = HingeCalculator.ComputeSpanTarget(first, second);
        var firstPanel = GetFirstPanel(topology);
        var secondPanel = ReferenceEquals(firstPanel, first) ? second : first;

        return layout switch
        {
            WindowLayoutKind.Span => new WindowLayoutTarget(span.Left, span.Top, span.Width, span.Height),
            WindowLayoutKind.Left => ToTarget(firstPanel),
            WindowLayoutKind.Right => ToTarget(secondPanel),
            WindowLayoutKind.Left70Right30 => Split(span, topology.Hinge.IsVertical, 70),
            WindowLayoutKind.Left30Right70 => Split(span, topology.Hinge.IsVertical, 30),
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unknown window layout.")
        };
    }

    private static DisplayInfo GetFirstPanel(DuoDisplayTopology topology) =>
        topology.Hinge.IsVertical
            ? topology.Hinge.DisplayA.X <= topology.Hinge.DisplayB.X ? topology.Hinge.DisplayA : topology.Hinge.DisplayB
            : topology.Hinge.DisplayA.Y <= topology.Hinge.DisplayB.Y ? topology.Hinge.DisplayA : topology.Hinge.DisplayB;

    private static WindowLayoutTarget ToTarget(DisplayInfo display) =>
        new(display.X, display.Y, display.Width, display.Height);

    private static WindowLayoutTarget Split(SpanTarget span, bool isVertical, int firstPercent)
    {
        if (isVertical)
        {
            var width = span.Width * firstPercent / 100;
            return new WindowLayoutTarget(span.Left, span.Top, width, span.Height);
        }

        var height = span.Height * firstPercent / 100;
        return new WindowLayoutTarget(span.Left, span.Top, span.Width, height);
    }
}
