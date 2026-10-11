namespace MoveMouse.Models;

/// <summary>
/// User-configurable preferences for Move Mouse. Instances are persisted as
/// JSON in the user's local application data folder by
/// <see cref="Services.ISettingsService"/>.
/// </summary>
public sealed class AppSettings
{
    /// <summary>How often, in seconds, the cursor should be nudged while running.</summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>The movement pattern applied on every jiggle tick.</summary>
    public MovementMode MovementMode { get; set; } = MovementMode.RandomMovement;

    /// <summary>The maximum distance, in pixels, the cursor moves on each tick.</summary>
    public int MovementDistancePixels { get; set; } = 6;

    /// <summary>
    /// When true, the system display is also kept on (<c>ES_DISPLAY_REQUIRED</c>).
    /// When false, only system sleep is prevented and the display may still turn off.
    /// </summary>
    public bool KeepDisplayOn { get; set; } = true;

    /// <summary>When true, closing the main window hides it to the system tray instead of exiting.</summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>When true, the application starts hidden in the system tray.</summary>
    public bool StartMinimizedToTray { get; set; }

    /// <summary>When true, jiggling begins automatically as soon as the application starts.</summary>
    public bool AutoStartJigglingOnLaunch { get; set; }

    /// <summary>When true, Move Mouse registers itself to launch at Windows sign-in.</summary>
    public bool LaunchAtWindowsStartup { get; set; }

    /// <summary>Preferred app theme: "Default" follows the system theme.</summary>
    public string Theme { get; set; } = "Default";

    /// <summary>When true, Move Mouse will shut down or sleep the PC on the configured schedule.</summary>
    public bool ScheduledActionEnabled { get; set; }

    /// <summary>Which power action the schedule performs.</summary>
    public ScheduledActionType ScheduledActionType { get; set; } = ScheduledActionType.Sleep;

    /// <summary>Whether the schedule fires at a fixed time each day or after a one-time countdown.</summary>
    public ScheduledActionTriggerMode ScheduledActionTriggerMode { get; set; } = ScheduledActionTriggerMode.AtSpecificTime;

    /// <summary>
    /// For <see cref="Models.ScheduledActionTriggerMode.AtSpecificTime"/>: the time of day the
    /// action runs, stored as minutes since midnight (0-1439) rather than
    /// <see cref="TimeSpan"/> for simple, unambiguous JSON round-tripping.
    /// </summary>
    public int ScheduledActionTimeOfDayMinutes { get; set; } = 22 * 60;

    /// <summary>For <see cref="Models.ScheduledActionTriggerMode.AfterCountdown"/>: minutes to count down before running.</summary>
    public int ScheduledActionCountdownMinutes { get; set; } = 60;

    /// <summary>
    /// Returns a deep-enough copy for safe comparison/rollback purposes.
    /// </summary>
    public AppSettings Clone() => new()
    {
        IntervalSeconds = IntervalSeconds,
        MovementMode = MovementMode,
        MovementDistancePixels = MovementDistancePixels,
        KeepDisplayOn = KeepDisplayOn,
        MinimizeToTrayOnClose = MinimizeToTrayOnClose,
        StartMinimizedToTray = StartMinimizedToTray,
        AutoStartJigglingOnLaunch = AutoStartJigglingOnLaunch,
        LaunchAtWindowsStartup = LaunchAtWindowsStartup,
        Theme = Theme,
        ScheduledActionEnabled = ScheduledActionEnabled,
        ScheduledActionType = ScheduledActionType,
        ScheduledActionTriggerMode = ScheduledActionTriggerMode,
        ScheduledActionTimeOfDayMinutes = ScheduledActionTimeOfDayMinutes,
        ScheduledActionCountdownMinutes = ScheduledActionCountdownMinutes
    };
}
