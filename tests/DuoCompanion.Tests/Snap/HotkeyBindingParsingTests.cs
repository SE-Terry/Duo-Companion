using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class HotkeyBindingParsingTests
{
    [Fact]
    public void Parse_accepts_a_valid_combination()
    {
        var binding = HotkeyBinding.Parse("Ctrl+Alt+S");

        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, binding.Modifiers);
        Assert.Equal((uint)'S', binding.VirtualKey);
        Assert.Equal("Ctrl+Alt+S", binding.Text);
    }

    [Fact]
    public void Parse_is_case_insensitive_and_produces_an_equal_binding()
    {
        var upper = HotkeyBinding.Parse("CTRL+ALT+S");
        var lower = HotkeyBinding.Parse("ctrl+alt+s");

        Assert.Equal(upper.Modifiers, lower.Modifiers);
        Assert.Equal(upper.VirtualKey, lower.VirtualKey);
        Assert.Equal(upper, lower); // equality ignores Text, so this proves same physical combo
    }

    [Fact]
    public void Parse_accepts_named_keys_and_function_keys()
    {
        Assert.Equal(0x25u, HotkeyBinding.Parse("Ctrl+Left").VirtualKey);
        Assert.Equal(0x70u, HotkeyBinding.Parse("Ctrl+F1").VirtualKey);
    }

    [Fact]
    public void Parse_rejects_a_repeated_modifier()
    {
        Assert.Throws<FormatException>(() => HotkeyBinding.Parse("Ctrl+Ctrl+S"));
    }

    [Fact]
    public void Parse_rejects_more_than_one_key()
    {
        Assert.Throws<FormatException>(() => HotkeyBinding.Parse("Ctrl+S+D"));
    }

    [Fact]
    public void Parse_rejects_an_unknown_key()
    {
        Assert.Throws<FormatException>(() => HotkeyBinding.Parse("Ctrl+Alt+NotAKey"));
    }

    [Fact]
    public void Parse_rejects_a_binding_with_no_key()
    {
        Assert.Throws<FormatException>(() => HotkeyBinding.Parse("Ctrl+Alt"));
    }

    [Fact]
    public void Parse_rejects_empty_text()
    {
        Assert.Throws<ArgumentException>(() => HotkeyBinding.Parse(""));
        Assert.Throws<ArgumentException>(() => HotkeyBinding.Parse("   "));
    }

    [Fact]
    public void Resolve_rejects_a_binding_that_duplicates_one_already_assigned_to_another_action()
    {
        var raw = new Dictionary<WindowLayoutKind, string>
        {
            [WindowLayoutKind.Span] = "Ctrl+Alt+S",
            [WindowLayoutKind.Left] = "ctrl+alt+s",
        };

        var resolution = HotkeyBindingResolver.Resolve(raw);

        Assert.Single(resolution.Bindings);
        Assert.True(resolution.Bindings.ContainsKey(WindowLayoutKind.Span));
        Assert.False(resolution.Bindings.ContainsKey(WindowLayoutKind.Left));

        var error = Assert.Single(resolution.Errors);
        Assert.Equal(WindowLayoutKind.Left, error.Action);
        Assert.Contains("Duplicate", error.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_reports_unparsable_bindings_without_throwing()
    {
        var raw = new Dictionary<WindowLayoutKind, string>
        {
            [WindowLayoutKind.Span] = "Ctrl+Alt+S",
            [WindowLayoutKind.Right] = "NotAValidBinding",
        };

        var resolution = HotkeyBindingResolver.Resolve(raw);

        Assert.Single(resolution.Bindings);
        var error = Assert.Single(resolution.Errors);
        Assert.Equal(WindowLayoutKind.Right, error.Action);
    }

    [Fact]
    public void Resolve_skips_blank_or_unassigned_bindings_without_errors()
    {
        var raw = new Dictionary<WindowLayoutKind, string>
        {
            [WindowLayoutKind.Span] = "",
            [WindowLayoutKind.Left] = "   ",
        };

        var resolution = HotkeyBindingResolver.Resolve(raw);

        Assert.Empty(resolution.Bindings);
        Assert.Empty(resolution.Errors);
    }
}
