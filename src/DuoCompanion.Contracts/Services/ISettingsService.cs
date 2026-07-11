using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    void Reset();
}
