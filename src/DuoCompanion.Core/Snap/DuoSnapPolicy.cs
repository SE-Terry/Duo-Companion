using DuoCompanion.Core.Models;

namespace DuoCompanion.Core.Snap;

public static class DuoSnapPolicy
{
    public static bool CanSpan(
        DuoSnapSettings settings,
        string? executableName,
        bool isTopologyUnambiguous,
        bool dwellComplete)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.AutoSpanEnabled &&
               isTopologyUnambiguous &&
               dwellComplete &&
               !IsIgnored(settings, executableName);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="executableName"/> is
    /// excluded from auto-span by either <see cref="DuoSnapSettings.IgnoredExecutableNames"/>
    /// or a matching <see cref="AppLayoutProfile.IsIgnored"/> profile. Exposed
    /// separately from <see cref="CanSpan"/> so callers that need only the
    /// ignore-list gate (e.g. a gesture path that intentionally bypasses the
    /// <see cref="DuoSnapSettings.AutoSpanEnabled"/>/dwell/topology checks) can
    /// reuse the same ignore logic instead of duplicating it.
    /// </summary>
    public static bool IsIgnored(DuoSnapSettings settings, string? executableName)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.IgnoredExecutableNames.Any(name =>
            string.Equals(name, executableName, StringComparison.OrdinalIgnoreCase)) ||
        settings.Profiles.Any(profile =>
            profile.IsIgnored && profile.MatchesExecutable(executableName));
    }
}
