using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

/// <summary>
/// Resolves and applies per-application layout profiles. Profiles match an
/// executable name (case-insensitively) to either a default
/// <see cref="WindowLayoutKind"/> or an exclusion (<see cref="AppLayoutProfile.IsIgnored"/>).
/// A profile is applied at most once per qualifying completion (e.g. a single
/// drag-end) — callers must invoke <see cref="ApplyIfMatched"/> only once per
/// completion, never repeatedly while a window is still being dragged.
/// </summary>
public interface IAppLayoutProfileService
{
    /// <summary>
    /// Returns the configured profile for <paramref name="executableName"/>, or
    /// <see langword="null"/> when no profile matches. Matching is case-insensitive
    /// and exact (no wildcards). An ignored profile is still returned so callers
    /// can distinguish "no profile" from "explicitly excluded".
    /// </summary>
    AppLayoutProfile? Resolve(string? executableName);

    /// <summary>
    /// Applies the configured layout for <paramref name="executableName"/> to
    /// <paramref name="hwnd"/> when a non-ignored profile matches and the current
    /// hinge topology is unambiguous. Returns <see langword="true"/> when a
    /// layout was applied.
    /// </summary>
    bool ApplyIfMatched(IntPtr hwnd, string? executableName);
}
