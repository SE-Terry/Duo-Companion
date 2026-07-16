using DuoCompanion.Core.Models;

namespace DuoCompanion.Core.Snap;

public static class HingeCalculator
{
    public const int DefaultActivationHalfWidth = 30;

    public static HingeZone? ComputeHingeZone(
        IReadOnlyList<DisplayInfo> displays, int activationHalfWidth = DefaultActivationHalfWidth)
    {
        return ComputeDuoTopology(displays, activationHalfWidth)?.Hinge;
    }

    public static DuoDisplayTopology? ComputeDuoTopology(
        IReadOnlyList<DisplayInfo> displays, int activationHalfWidth = DefaultActivationHalfWidth)
    {
        var candidates = new List<HingeZone>();

        for (var i = 0; i < displays.Count - 1; i++)
        {
            for (var j = i + 1; j < displays.Count; j++)
            {
                var a = displays[i];
                var b = displays[j];

                if (SharesVerticalEdge(a, b))
                {
                    candidates.Add(new HingeZone(
                        a, b, IsVertical: true, ActivationCenter: Math.Max(a.X, b.X),
                        ActivationHalfWidth: activationHalfWidth));
                }
                else if (SharesHorizontalEdge(a, b))
                {
                    candidates.Add(new HingeZone(
                        a, b, IsVertical: false, ActivationCenter: Math.Max(a.Y, b.Y),
                        ActivationHalfWidth: activationHalfWidth));
                }
            }
        }

        if (candidates.Count != 1) return null;

        return new DuoDisplayTopology(candidates[0], HasExternalDisplays: displays.Count > 2);
    }

    private static bool SharesVerticalEdge(DisplayInfo a, DisplayInfo b) =>
        a.Y == b.Y &&
        a.Height == b.Height &&
        (a.X + a.Width == b.X || b.X + b.Width == a.X);

    private static bool SharesHorizontalEdge(DisplayInfo a, DisplayInfo b) =>
        a.X == b.X &&
        a.Width == b.Width &&
        (a.Y + a.Height == b.Y || b.Y + b.Height == a.Y);

    public static SpanTarget ComputeSpanTarget(DisplayInfo a, DisplayInfo b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new SpanTarget(left, top, right - left, bottom - top);
    }
}
