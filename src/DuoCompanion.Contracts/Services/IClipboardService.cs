using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IClipboardService
{
    IReadOnlyList<ClipboardItem> Items { get; }
    event EventHandler ItemsChanged;
    void Initialize();
    void Pin(Guid id);
    void Remove(Guid id);
    void Clear();
    Task PasteAsync(Guid id);
}
