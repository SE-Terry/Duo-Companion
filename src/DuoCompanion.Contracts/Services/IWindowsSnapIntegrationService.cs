using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

/// <summary>
/// Gates auto-span and explicit layout-command behavior by the configured
/// <see cref="SnapIntegrationMode"/>, and owns the lifecycle that discards
/// stored restore rectangles for windows that are destroyed. Never reads or
/// mutates any Windows Snap OS setting in any mode.
/// </summary>
public interface IWindowsSnapIntegrationService
{
    bool CanAutoSpan(SnapIntegrationMode mode);
    bool CanApplyLayoutCommand(SnapIntegrationMode mode);
    void Start();
    void Stop();
}
