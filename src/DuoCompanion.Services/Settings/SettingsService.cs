using System.Text.Json;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DuoCompanion", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;

    public AppSettings Current { get; private set; }
    public event EventHandler? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
        : this(logger, SettingsPath)
    {
    }

    internal SettingsService(ILogger<SettingsService> logger, string settingsPath)
    {
        _logger = logger;
        _settingsPath = settingsPath;
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.DuoSnap ??= new DuoSnapSettings();
            settings.DuoSnap.Normalize();
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings — using defaults");
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Current.DuoSnap ??= new DuoSnapSettings();
            Current.DuoSnap.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Current, JsonOptions));
            _logger.LogInformation("Settings saved to {Path}", _settingsPath);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void Reset()
    {
        Current = new AppSettings();
        Save();
    }
}
