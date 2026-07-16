namespace DuoCompanion.Core.Models;

public enum WindowLayoutKind
{
    Left,
    Right,
    Span,
    Left70Right30,
    Left30Right70
}

public sealed record WindowLayoutTarget(int Left, int Top, int Width, int Height);
