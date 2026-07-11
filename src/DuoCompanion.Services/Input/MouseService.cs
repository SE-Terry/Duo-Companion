using System.Runtime.InteropServices;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;

namespace DuoCompanion.Services.Input;

public sealed class MouseService : IMouseService
{
    public void MoveDelta(int dx, int dy) =>
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.INPUTUNION
            {
                mi = new NativeMethods.MOUSEINPUT { dx = dx, dy = dy, dwFlags = NativeMethods.MOUSEEVENTF_MOVE }
            }
        });

    public void Click(MouseButton button)
    {
        var (down, up) = button == MouseButton.Left
            ? (NativeMethods.MOUSEEVENTF_LEFTDOWN, NativeMethods.MOUSEEVENTF_LEFTUP)
            : (NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP);

        Send(new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new() { mi = new() { dwFlags = down } } });
        Send(new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new() { mi = new() { dwFlags = up } } });
    }

    public void ScrollDelta(int wheelDelta) =>
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new() { mi = new() { mouseData = (uint)wheelDelta, dwFlags = NativeMethods.MOUSEEVENTF_WHEEL } }
        });

    private static void Send(NativeMethods.INPUT input)
    {
        var inputs = new[] { input };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
