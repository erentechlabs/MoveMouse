using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>
/// Performs an actual, immediate system power action (shutdown or sleep).
/// Deliberately kept as its own tiny, single-purpose service so the
/// higher-level scheduling logic in <see cref="IScheduledPowerActionService"/>
/// never has to deal with native/process-launch concerns directly.
/// </summary>
public interface IPowerActionExecutor
{
    /// <summary>Executes <paramref name="action"/> immediately. Returns true if the request was issued successfully.</summary>
    bool Execute(ScheduledActionType action);
}
