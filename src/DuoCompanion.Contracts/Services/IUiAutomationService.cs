namespace DuoCompanion.Contracts.Services;

public interface IUiAutomationService
{
    event EventHandler TextInputFocused;
    event EventHandler TextInputBlurred;
    void Start(IntPtr hostHwnd);
    void Stop();
    void SuppressBriefly();
}
