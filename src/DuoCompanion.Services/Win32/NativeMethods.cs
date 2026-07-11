using System.Runtime.InteropServices;
using System.Text;

namespace DuoCompanion.Services.Win32;

internal static class NativeMethods
{
    // --- Display enumeration (Milestone 1) ---

    internal const uint MONITORINFOF_PRIMARY = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    // --- Window positioning + display change hook (Task 1) ---

    internal const int SWP_NOMOVE = 0x0002;
    internal const int SWP_NOSIZE = 0x0001;
    internal const int SWP_NOACTIVATE = 0x0010;
    internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    internal const uint EVENT_SYSTEM_DISPLAYCHANGE = 0x001B;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc,
        uint idProcess, uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    internal delegate void WinEventProc(
        IntPtr hWinEventHook, uint @event,
        IntPtr hwnd, int idObject, int idChild,
        uint idEventThread, uint dwmsEventTime);

    // --- Keyboard/mouse injection (Tasks 3 + 9) ---

    internal const uint INPUT_MOUSE = 0;
    internal const uint INPUT_KEYBOARD = 1;

    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    internal const uint KEYEVENTF_UNICODE = 0x0004;

    internal const uint MOUSEEVENTF_MOVE = 0x0001;
    internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
    internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    internal const uint MOUSEEVENTF_WHEEL = 0x0800;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    // --- UI Automation focus tracking (Task 5) ---

    internal const uint EVENT_OBJECT_FOCUS = 0x8005;
    internal const uint EVENT_OBJECT_STATECHANGE = 0x800A;
    internal const int OBJID_CLIENT = 0x00000000;

    [DllImport("oleacc.dll")]
    internal static extern int AccessibleObjectFromEvent(
        IntPtr hwnd, int idObject, int idChild,
        [MarshalAs(UnmanagedType.IUnknown)] out object? ppvObject,
        out int varChild);

    [DllImport("oleacc.dll")]
    internal static extern int GetRoleText(uint dwRole, StringBuilder lpszRole, uint cchRoleMax);
}
