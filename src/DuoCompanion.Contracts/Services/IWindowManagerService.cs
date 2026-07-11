namespace DuoCompanion.Contracts.Services;

public interface IWindowManagerService
{
    void MakeWindowNonActivating(IntPtr hwnd);
    void PositionCompanionWindow(IntPtr hwnd);
    event EventHandler DisplayConfigurationChanged;
    void StartMonitoring(IntPtr hostHwnd);
    void StopMonitoring();
}
