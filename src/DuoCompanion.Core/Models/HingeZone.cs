namespace DuoCompanion.Core.Models;

public sealed record HingeZone(
    DisplayInfo DisplayA,
    DisplayInfo DisplayB,
    bool IsVertical,
    int ActivationCenter,
    int ActivationHalfWidth)
{
    public bool Contains(int x, int y) =>
        IsVertical
            ? x >= ActivationCenter - ActivationHalfWidth && x <= ActivationCenter + ActivationHalfWidth
            : y >= ActivationCenter - ActivationHalfWidth && y <= ActivationCenter + ActivationHalfWidth;
}
