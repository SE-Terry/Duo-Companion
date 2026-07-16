using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;

namespace DuoCompanion.Services.Snap;

public sealed class DuoSnapSettingsMonitor : IDuoSnapSettingsMonitor, IDisposable
{
    private readonly ISettingsService _settings;

    public DuoSnapSettings Current => _settings.Current.DuoSnap;
    public event EventHandler? Changed;

    public DuoSnapSettingsMonitor(ISettingsService settings)
    {
        _settings = settings;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;
}
