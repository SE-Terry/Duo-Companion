namespace DuoCompanion.Contracts.Services;

public interface IWindowManagerService
{
    void PositionOnSecondaryDisplay(IntPtr hwnd);
    event EventHandler DisplayConfigurationChanged;
    void StartMonitoring(IntPtr hostHwnd);
    void StopMonitoring();
}
