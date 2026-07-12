using System.Runtime.InteropServices;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Input;

public sealed class InputService : IInputService
{
    private readonly ILogger<InputService> _logger;
    private readonly IUiAutomationService _automation;

    public InputService(ILogger<InputService> logger, IUiAutomationService automation)
    {
        _logger = logger;
        _automation = automation;
    }

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
            Send(inputs);
        }
        _logger.LogDebug("Sent text of {Length} chars", text.Length);
    }

    private void Send(NativeMethods.INPUT input) => Send(new[] { input });

    private void Send(NativeMethods.INPUT[] inputs)
    {
        _automation.SuppressBriefly();

        var sent = NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());

        if (sent != (uint)inputs.Length)
        {
            _logger.LogWarning(
                "SendInput accepted {Sent} of {Requested} input events (Win32 error {Error})",
                sent,
                inputs.Length,
                Marshal.GetLastWin32Error());
        }
    }
}
