using System.Runtime.InteropServices;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Display;

public sealed class DisplayService : IDisplayService
{
    private readonly ILogger<DisplayService> _logger;

    public DisplayService(ILogger<DisplayService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DisplayInfo> GetAllDisplays()
    {
        var displays = new List<DisplayInfo>();
        var index = 0;

        bool EnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
            };

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                var display = new DisplayInfo(
                    Index: index++,
                    DeviceName: info.szDevice.TrimEnd('\0'),
                    X: info.rcMonitor.Left,
                    Y: info.rcMonitor.Top,
                    Width: info.rcMonitor.Right - info.rcMonitor.Left,
                    Height: info.rcMonitor.Bottom - info.rcMonitor.Top,
                    IsPrimary: (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0
                );

                displays.Add(display);
                _logger.LogInformation("Detected: {Display}", display);
            }

            return true;
        }

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, IntPtr.Zero);

        _logger.LogInformation("Total displays found: {Count}", displays.Count);
        return displays.AsReadOnly();
    }

    public DisplayInfo? GetPrimaryDisplay() =>
        GetAllDisplays().FirstOrDefault(d => d.IsPrimary);

    public DisplayInfo? GetSecondaryDisplay() =>
        GetAllDisplays().FirstOrDefault(d => d.IsSecondary);
}
