using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,     // MOD_ALT
    Control = 0x0002, // MOD_CONTROL
    Shift = 0x0004,   // MOD_SHIFT
    Win = 0x0008,     // MOD_WIN
}

/// <summary>
/// An immutable, parsed representation of a global hotkey binding such as
/// "Ctrl+Alt+S". <see cref="Text"/> preserves the original (unnormalized) input
/// for display and persistence; <see cref="Modifiers"/>/<see cref="VirtualKey"/>
/// participate in value equality so two strings that describe the same physical
/// key combination (regardless of casing or modifier order) compare equal.
/// </summary>
public sealed record HotkeyBinding(HotkeyModifiers Modifiers, uint VirtualKey, string Text)
{
    public bool Equals(HotkeyBinding? other) =>
        other is not null && Modifiers == other.Modifiers && VirtualKey == other.VirtualKey;

    public override int GetHashCode() => HashCode.Combine(Modifiers, VirtualKey);

    /// <summary>
    /// Parses a '+'-separated hotkey binding grammar, e.g. "Ctrl+Alt+S". Modifier
    /// tokens (Ctrl/Control, Alt, Shift, Win/Windows) are case-insensitive and may
    /// appear in any order; exactly one non-modifier token identifies the key
    /// (A-Z, 0-9, F1-F24, or one of the named keys Left/Right/Up/Down/Space/Tab/
    /// Enter/Escape). Throws <see cref="FormatException"/> for a repeated
    /// modifier, more than one key, an unrecognized token, or a binding with no
    /// key, and <see cref="ArgumentException"/> for null/blank input.
    /// </summary>
    public static HotkeyBinding Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new FormatException($"'{text}' is not a valid hotkey binding.");

        var modifiers = HotkeyModifiers.None;
        uint? virtualKey = null;

        foreach (var part in parts)
        {
            if (TryParseModifier(part, out var modifier))
            {
                if (modifiers.HasFlag(modifier))
                    throw new FormatException($"'{text}' repeats the '{part}' modifier.");
                modifiers |= modifier;
            }
            else if (TryParseKey(part, out var vk))
            {
                if (virtualKey is not null)
                    throw new FormatException($"'{text}' specifies more than one key.");
                virtualKey = vk;
            }
            else
            {
                throw new FormatException($"'{part}' in '{text}' is not a recognized modifier or key.");
            }
        }

        if (virtualKey is null)
            throw new FormatException($"'{text}' does not specify a key.");

        return new HotkeyBinding(modifiers, virtualKey.Value, text);
    }

    private static bool TryParseModifier(string token, out HotkeyModifiers modifier)
    {
        modifier = token.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => HotkeyModifiers.Control,
            "ALT" => HotkeyModifiers.Alt,
            "SHIFT" => HotkeyModifiers.Shift,
            "WIN" or "WINDOWS" => HotkeyModifiers.Win,
            _ => HotkeyModifiers.None,
        };
        return modifier != HotkeyModifiers.None;
    }

    private static bool TryParseKey(string token, out uint virtualKey) =>
        NamedKeys.TryGetValue(token.ToUpperInvariant(), out virtualKey);

    private static readonly IReadOnlyDictionary<string, uint> NamedKeys = BuildNamedKeys();

    private static IReadOnlyDictionary<string, uint> BuildNamedKeys()
    {
        var map = new Dictionary<string, uint>();
        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = c;      // VK_A..VK_Z == 'A'..'Z'
        for (var d = '0'; d <= '9'; d++) map[d.ToString()] = d;      // VK_0..VK_9 == '0'..'9'
        for (var f = 1; f <= 24; f++) map[$"F{f}"] = (uint)(0x70 + (f - 1)); // VK_F1 = 0x70
        map["LEFT"] = 0x25;
        map["UP"] = 0x26;
        map["RIGHT"] = 0x27;
        map["DOWN"] = 0x28;
        map["SPACE"] = 0x20;
        map["TAB"] = 0x09;
        map["ENTER"] = 0x0D;
        map["ESCAPE"] = 0x1B;
        map["ESC"] = 0x1B;
        return map;
    }
}

/// <summary>One entry rejected while resolving configured hotkey bindings.</summary>
public sealed record HotkeyBindingError(WindowLayoutKind Action, string Text, string Reason);

/// <summary>
/// Pure resolution of a raw (per-action) hotkey binding configuration into
/// parsed, non-conflicting bindings. Two different actions that parse to the
/// same physical key combination are a conflict: the action encountered first
/// keeps the binding and every later duplicate is reported as an error rather
/// than silently registered (which would race unpredictably at the OS level).
/// Blank/missing entries mean "no hotkey assigned" and are skipped silently.
/// </summary>
public static class HotkeyBindingResolver
{
    public sealed record Resolution(
        IReadOnlyDictionary<WindowLayoutKind, HotkeyBinding> Bindings,
        IReadOnlyList<HotkeyBindingError> Errors);

    public static Resolution Resolve(IReadOnlyDictionary<WindowLayoutKind, string> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var bindings = new Dictionary<WindowLayoutKind, HotkeyBinding>();
        var errors = new List<HotkeyBindingError>();
        var owners = new Dictionary<HotkeyBinding, WindowLayoutKind>();

        foreach (var (action, text) in raw)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            HotkeyBinding parsed;
            try
            {
                parsed = HotkeyBinding.Parse(text);
            }
            catch (FormatException ex)
            {
                errors.Add(new HotkeyBindingError(action, text, ex.Message));
                continue;
            }

            if (owners.TryGetValue(parsed, out var owner))
            {
                errors.Add(new HotkeyBindingError(
                    action, text, $"Duplicate of the binding already assigned to {owner}."));
                continue;
            }

            owners[parsed] = action;
            bindings[action] = parsed;
        }

        return new Resolution(bindings, errors);
    }
}

/// <summary>The outcome of attempting to register (or resolve) one configured hotkey.</summary>
public sealed class HotkeyRegistrationResult
{
    public WindowLayoutKind Action { get; }
    public string BindingText { get; }
    public bool Succeeded { get; }
    public string? Error { get; }

    public HotkeyRegistrationResult(WindowLayoutKind action, string bindingText, bool succeeded, string? error = null)
    {
        Action = action;
        BindingText = bindingText;
        Succeeded = succeeded;
        Error = error;
    }
}

public sealed class HotkeyInvokedEventArgs : EventArgs
{
    public WindowLayoutKind Action { get; }
    public HotkeyInvokedEventArgs(WindowLayoutKind action) => Action = action;
}

/// <summary>
/// Registers global hotkeys (one per configured <see cref="WindowLayoutKind"/>
/// action) via a hidden message-only window, and applies the corresponding
/// layout to the current foreground window when one fires. Conflicts (either a
/// duplicate binding across actions, or the OS reporting the combination is
/// already registered by another application) are never forced — they are
/// surfaced via <see cref="RegistrationResults"/> and logged, and simply leave
/// that one action's hotkey unavailable. Gated the same way every other
/// explicit layout command is: <see cref="IWindowsSnapIntegrationService.CanApplyLayoutCommand"/>.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>The outcome of every configured binding as of the last <see cref="Start"/>.</summary>
    IReadOnlyList<HotkeyRegistrationResult> RegistrationResults { get; }

    /// <summary>Raised whenever a registered hotkey fires, before the layout is applied.</summary>
    event EventHandler<HotkeyInvokedEventArgs>? HotkeyInvoked;

    /// <summary>Idempotent: creates the message-bridge window and registers all configured hotkeys.</summary>
    void Start();

    /// <summary>Idempotent: unregisters every hotkey and destroys the message-bridge window.</summary>
    void Stop();
}
