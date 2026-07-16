using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public sealed class LayoutSuggestedEventArgs : EventArgs
{
    public IntPtr Hwnd { get; }
    public WindowLayoutKind Layout { get; }

    public LayoutSuggestedEventArgs(IntPtr hwnd, WindowLayoutKind layout)
    {
        Hwnd = hwnd;
        Layout = layout;
    }
}

/// <summary>
/// Suggests a layout for windows that don't already have an explicit
/// <see cref="AppLayoutProfile"/>. Suggestions are deterministic, conservative
/// (browser/document/file-manager executables only), and preview-only:
/// <see cref="Evaluate"/> never moves a window, it only raises
/// <see cref="LayoutSuggested"/>. A suggestion is only applied when a caller
/// explicitly confirms it via <see cref="ApplySuggestedLayout"/>.
/// </summary>
public interface ILayoutSuggestionService
{
    event EventHandler<LayoutSuggestedEventArgs>? LayoutSuggested;

    /// <summary>
    /// Evaluates whether <paramref name="hwnd"/> (owned by <paramref name="executableName"/>)
    /// qualifies for a layout suggestion. Raises <see cref="LayoutSuggested"/> when it does.
    /// Never applies a layout. No suggestion is raised when an <see cref="IAppLayoutProfileService"/>
    /// profile already matches the executable (a configured profile — applied or ignored — always
    /// overrides a suggestion).
    /// </summary>
    void Evaluate(IntPtr hwnd, string? executableName);

    /// <summary>
    /// Applies the most recently suggested layout for <paramref name="hwnd"/>, if any. No-op when
    /// there is no pending suggestion for that window or the hinge topology is currently ambiguous.
    /// </summary>
    void ApplySuggestedLayout(IntPtr hwnd);

    /// <summary>Discards any pending suggestion for a window that no longer exists.</summary>
    void ForgetWindow(IntPtr hwnd);
}
