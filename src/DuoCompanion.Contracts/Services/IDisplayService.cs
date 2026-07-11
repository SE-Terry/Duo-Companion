using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IDisplayService
{
    IReadOnlyList<DisplayInfo> GetAllDisplays();
    DisplayInfo? GetPrimaryDisplay();
    DisplayInfo? GetSecondaryDisplay();
}
