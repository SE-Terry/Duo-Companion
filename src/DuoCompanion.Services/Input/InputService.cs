using System.Runtime.InteropServices;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Input;

public sealed class InputService : IInputService
{
    private readonly ILogger<InputService> _logger;

    public InputService(ILogger<InputService> logger) => _logger = logger;

    public void SendKey(ushort virtualKeyCode, bool isExtendedKey = false)
    {
        SendKeyDown(virtualKeyCode, isExtendedKey);
        SendKeyUp(virtualKeyCode, isExtendedKey);
    }

    public void SendKeyDown(ushort virtualKeyCode) => SendKeyDown(virtualKeyCode, false);
    public void SendKeyUp(ushort virtualKeyCode) => SendKeyUp(virtualKeyCode, false);

    private void SendKeyDown(ushort vk, bool extended)
    {
        var flags = extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u;
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = flags }
            }
        });
    }

    private void SendKeyUp(ushort vk, bool extended)
    {
        var flags = NativeMethods.KEYEVENTF_KEYUP | (extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u);
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = flags }
            }
        });
    }

    public void SendText(string text)
    {
        foreach (var ch in text)
        {
            var inputs = new[]
            {
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    u = new NativeMethods.INPUTUNION
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE
                        }
                    }
                },
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    u = new NativeMethods.INPUTUNION
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
                        }
                    }
                }
            };
            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }
        _logger.LogDebug("Sent text of {Length} chars", text.Length);
    }

    private static void Send(NativeMethods.INPUT input)
    {
        var inputs = new[] { input };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
