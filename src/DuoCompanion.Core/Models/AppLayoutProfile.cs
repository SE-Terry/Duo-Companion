namespace DuoCompanion.Core.Models;

public sealed class AppLayoutProfile
{
    public string ExecutableName { get; set; } = string.Empty;
    public WindowLayoutKind Layout { get; set; } = WindowLayoutKind.Span;
    public bool IsIgnored { get; set; }

    public bool MatchesExecutable(string? executableName) =>
        !string.IsNullOrWhiteSpace(ExecutableName) &&
        string.Equals(ExecutableName, executableName, StringComparison.OrdinalIgnoreCase);
}
