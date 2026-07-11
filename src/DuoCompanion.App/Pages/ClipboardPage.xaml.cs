using DuoCompanion.App.ViewModels;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace DuoCompanion.App.Pages;

public sealed partial class ClipboardPage : Page
{
    private readonly IClipboardService _clipboard;
    private string _searchQuery = string.Empty;

    public ClipboardPage()
    {
        _clipboard = App.Services.GetRequiredService<IClipboardService>();
        InitializeComponent();
        _clipboard.ItemsChanged += OnItemsChanged;
        Loaded += (_, _) => RefreshList();
        Unloaded += (_, _) => _clipboard.ItemsChanged -= OnItemsChanged;
    }

    private void OnItemsChanged(object? sender, EventArgs e) => RefreshList();

    private void RefreshList()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var items = _clipboard.Items
                .Where(i => string.IsNullOrEmpty(_searchQuery) ||
                            i.Text.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                .Select(ClipboardItemViewModel.From)
                .ToList();
            HistoryList.ItemsSource = items;
        });
    }

    private async void OnPasteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) await _clipboard.PasteAsync(id);
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) _clipboard.Pin(id);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) _clipboard.Remove(id);
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => _clipboard.Clear();

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text;
        RefreshList();
    }
}
