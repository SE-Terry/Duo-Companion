namespace DuoCompanion.App.ViewModels;

internal sealed class DisplayViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
}
