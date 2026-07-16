namespace DuoCompanion.Contracts.Services;

public interface IWindowManagerService
{
    void MakeWindowNonActivating(IntPtr hwnd);
    void PositionCompanionWindow(IntPtr hwnd);
    void HideCompanionWindow(IntPtr hwnd);
    void ShowCompanionWindow(IntPtr hwnd);
    void MakeWindowClickThrough(IntPtr hwnd);
    void SetWindowOpacity(IntPtr hwnd, double opacity);
    void SetWindowBounds(IntPtr hwnd, int left, int top, int width, int height);
    event EventHandler DisplayConfigurationChanged;
    void StartMonitoring(IntPtr hostHwnd);
    void StopMonitoring();
}
