using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IDuoSnapSettingsMonitor
{
    DuoSnapSettings Current { get; }
    event EventHandler? Changed;
}
