namespace DuoCompanion.Contracts.Services;

public interface IWindowIdentityService
{
    string GetProcessName(IntPtr hwnd);
}
