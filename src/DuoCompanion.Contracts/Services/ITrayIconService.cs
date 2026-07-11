namespace DuoCompanion.Contracts.Services;

public interface ITrayIconService
{
    event EventHandler ToggleVisibilityRequested;
    event EventHandler QuitRequested;
    void Start();
    void Stop();
}
