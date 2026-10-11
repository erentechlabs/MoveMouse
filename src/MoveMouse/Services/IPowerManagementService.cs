namespace MoveMouse.Services;

/// <summary>
/// Controls Windows system power/idle behavior so the machine does not go to
/// sleep (and optionally keeps the display on) while Move Mouse is active.
/// </summary>
public interface IPowerManagementService
{
    /// <summary>True while sleep prevention is currently engaged.</summary>
    bool IsPreventingSleep { get; }

    /// <summary>
    /// Tells Windows the system (and optionally the display) should be kept
    /// awake. Safe to call repeatedly; the new flags simply replace the old
    /// ones.
    /// </summary>
    /// <param name="keepDisplayOn">
    /// When true, also prevents the display from turning off
    /// (<c>ES_DISPLAY_REQUIRED</c>). When false, only system sleep is
    /// prevented and the monitor may still turn off per its own timeout.
    /// </param>
    /// <returns>True if the Windows API call succeeded.</returns>
    bool PreventSleep(bool keepDisplayOn);

    /// <summary>
    /// Restores normal Windows power-management behavior. Always safe to
    /// call, including when sleep prevention was never engaged.
    /// </summary>
    /// <returns>True if the Windows API call succeeded.</returns>
    bool AllowSleep();
}
