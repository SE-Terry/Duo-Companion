using DuoCompanion.Core.Models;

namespace DuoCompanion.App.ViewModels;

internal sealed class ClipboardItemViewModel
{
    private const char PinnedGlyphChar = (char)0xE840;
    private const char UnpinnedGlyphChar = (char)0xE841;

    public Guid Id { get; init; }
    public string Preview { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
    public string PinGlyph => (IsPinned ? PinnedGlyphChar : UnpinnedGlyphChar).ToString();

    public static ClipboardItemViewModel From(ClipboardItem item) => new()
    {
        Id      = item.Id,
        Preview = item.Text.Length > 80 ? item.Text[..80] + "…" : item.Text,
        Time    = item.CapturedAt.ToLocalTime().ToString("HH:mm"),
        IsPinned = item.IsPinned
    };
}
