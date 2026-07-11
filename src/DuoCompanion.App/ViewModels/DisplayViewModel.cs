namespace DuoCompanion.App.ViewModels;

internal sealed class DisplayViewModel
{
    private const char PrimaryGlyphChar = (char)0xE7F4;
    private const char SecondaryGlyphChar = (char)0xE780;

    public string Label { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public string Glyph => (IsPrimary ? PrimaryGlyphChar : SecondaryGlyphChar).ToString();
}
