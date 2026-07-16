using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler? SettingsChanged;
    void Save();
    void Reset();
}
