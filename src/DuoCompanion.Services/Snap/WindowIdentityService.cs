using System.Diagnostics;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;

namespace DuoCompanion.Services.Snap;

public sealed class WindowIdentityService : IWindowIdentityService
{
    public string GetProcessName(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0) return string.Empty;

        try
        {
            using var process = Process.GetProcessById(unchecked((int)processId));
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
