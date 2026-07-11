using DuoCompanion.App.ViewModels;
using DuoCompanion.Contracts.Services;
using Microsoft.UI.Xaml;

namespace DuoCompanion.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(IDisplayService displayService)
    {
        InitializeComponent();

        var displays = displayService.GetAllDisplays()
            .Select(d => new DisplayViewModel
            {
                Label = d.IsPrimary ? "Primary Display" : $"Secondary Display #{d.Index}",
                Resolution = $"{d.Width} × {d.Height}",
                Position = $"Position: ({d.X}, {d.Y})",
                DeviceName = d.DeviceName,
                IsPrimary = d.IsPrimary
            })
            .ToList();

        DisplayList.ItemsSource = displays;
    }
}
