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
        Theme = Theme
    };
}
