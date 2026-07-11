namespace DuoCompanion.Core.Models;

public sealed record DisplayInfo(
    int Index,
    string DeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary)
{
    public bool IsSecondary => !IsPrimary;

    public override string ToString() =>
        $"[{Index}] {DeviceName.Trim()} {Width}×{Height} at ({X},{Y}) {(IsPrimary ? "(Primary)" : "(Secondary)")}";
}
