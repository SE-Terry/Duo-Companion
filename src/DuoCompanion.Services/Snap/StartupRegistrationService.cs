using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DuoCompanion.Services.Snap;

// Reads/writes exactly one named value under HKCU\...\Run. Ownership of that
// value is never tracked as in-memory session state (a flag would be wrong the
// moment the app restarts) — it is instead re-derived every time by comparing
// the value's current contents to this installation's own executable path, so
// a value that predates this app, or was written by something else under the
// same name, is always left untouched.
public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DuoCompanion";

    private readonly ILogger<StartupRegistrationService> _logger;

    public StartupRegistrationService(ILogger<StartupRegistrationService> logger) => _logger = logger;

    public bool IsRegistered
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var current = key?.GetValue(ValueName) as string;
                return current is not null && string.Equals(current, ExpectedCommand(), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read the startup registration state");
                return false;
            }
        }
    }

    public bool Apply(bool enabled) => enabled ? Register() : Remove();

    private bool Register()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                _logger.LogError("Failed to open {Path} for startup registration", RunKeyPath);
                return false;
            }

            key.SetValue(ValueName, ExpectedCommand());
            _logger.LogInformation("Startup registration set");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write the startup registration value");
            return false;
        }
    }

    private bool Remove()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return true; // Run key doesn't exist — nothing to remove.

            var current = key.GetValue(ValueName) as string;
            if (current is null) return true; // Already absent.

            if (!string.Equals(current, ExpectedCommand(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Leaving the existing '{Name}' startup registration value alone — its contents do not match this installation",
                    ValueName);
                return false;
            }

            key.DeleteValue(ValueName, throwOnMissingValue: false);
            _logger.LogInformation("Startup registration removed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove the startup registration value");
            return false;
        }
    }

    private static string ExpectedCommand()
    {
        var exePath = Environment.ProcessPath ?? string.Empty;
        return $"\"{exePath}\"";
    }
}
