using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;

namespace DuoCompanion.Services.Clipboard;

public sealed class ClipboardService : IClipboardService
{
    private const int MaxHistory = 50;

    private readonly IInputService _input;
    private readonly ILogger<ClipboardService> _logger;
    private readonly List<ClipboardItem> _items = new();
    private string? _lastText;

    public IReadOnlyList<ClipboardItem> Items => _items.AsReadOnly();
    public event EventHandler? ItemsChanged;

    public ClipboardService(IInputService input, ILogger<ClipboardService> logger)
    {
        _input  = input;
        _logger = logger;
    }

    public void Initialize()
    {
        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += OnClipboardChanged;
        _logger.LogInformation("Clipboard monitoring started");
    }

    private async void OnClipboardChanged(object? sender, object e)
    {
        try
        {
            var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text)) return;

            var text = await content.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text) || text == _lastText) return;

            _lastText = text;

            var item = new ClipboardItem(Guid.NewGuid(), text, DateTimeOffset.Now);

            // Remove duplicate text, keep pinned items at top
            _items.RemoveAll(i => !i.IsPinned && i.Text == text);

            var insertAt = _items.FindLastIndex(i => i.IsPinned) + 1;
            _items.Insert(insertAt, item);

            // Trim unpinned items over limit
            while (_items.Count(i => !i.IsPinned) > MaxHistory)
            {
                var lastUnpinned = _items.FindLastIndex(i => !i.IsPinned);
                if (lastUnpinned >= 0) _items.RemoveAt(lastUnpinned);
            }

            _logger.LogDebug("Clipboard item added: {Preview}", text[..Math.Min(40, text.Length)]);
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read clipboard content");
        }
    }

    public void Pin(Guid id)
    {
        var idx = _items.FindIndex(i => i.Id == id);
        if (idx < 0) return;
        _items[idx] = _items[idx] with { IsPinned = true };
        _items.Sort((a, b) => b.IsPinned.CompareTo(a.IsPinned));
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(Guid id)
    {
        _items.RemoveAll(i => i.Id == id);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _items.RemoveAll(i => !i.IsPinned);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task PasteAsync(Guid id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;

        var package = new DataPackage();
        package.SetText(item.Text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

        await Task.Delay(50); // brief delay for clipboard to settle
        _input.SendKeyDown(0x11); // Ctrl down
        _input.SendKey(0x56);     // V
        _input.SendKeyUp(0x11);   // Ctrl up

        _logger.LogInformation("Pasted clipboard item {Id}", id);
    }
}
