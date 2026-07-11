using DuoCompanion.Core.Models;

namespace DuoCompanion.App.ViewModels;

internal sealed class ClipboardItemViewModel
{
    public Guid Id { get; init; }
    public string Preview { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public bool IsPinned { get; init; }

    public static ClipboardItemViewModel From(ClipboardItem item) => new()
    {
        Id      = item.Id,
        Preview = item.Text.Length > 80 ? item.Text[..80] + "…" : item.Text,
        Time    = item.CapturedAt.ToLocalTime().ToString("HH:mm"),
        IsPinned = item.IsPinned
    };
}
