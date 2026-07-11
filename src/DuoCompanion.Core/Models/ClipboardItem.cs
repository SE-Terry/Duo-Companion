namespace DuoCompanion.Core.Models;

public sealed record ClipboardItem(
    Guid Id,
    string Text,
    DateTimeOffset CapturedAt)
{
    public bool IsPinned { get; init; }
}
