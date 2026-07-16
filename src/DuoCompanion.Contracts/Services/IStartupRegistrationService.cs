namespace DuoCompanion.Contracts.Services;

/// <summary>
/// Manages DuoCompanion's per-user Windows startup registration in
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>. Only ever
/// writes/removes the single named value this service owns for DuoCompanion,
/// and treats that value as "ours" solely by comparing its current contents to
/// this installation's own executable path — never by session-local state. A
/// value that predates this app, or was written by something else under the
/// same name, is left alone and reported as a failure rather than overwritten
/// or deleted.
/// </summary>
public interface IStartupRegistrationService
{
    /// <summary>True when the Run key currently has a value matching this executable.</summary>
    bool IsRegistered { get; }

    /// <summary>
    /// Reconciles the startup registration with <paramref name="enabled"/>: writes
    /// the Run value when true, removes it when false — but only removes it when
    /// its current contents still match this installation (see remarks on
    /// <see cref="IStartupRegistrationService"/>). Failures are logged and leave
    /// the prior registration state intact. Returns true on success.
    /// </summary>
    bool Apply(bool enabled);
}
