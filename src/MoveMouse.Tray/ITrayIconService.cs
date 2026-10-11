namespace MoveMouse.Tray;

/// <summary>
/// Manages the Move Mouse system tray icon, its context menu, and balloon
/// notifications, allowing the application to run in the background after
/// the main window is closed or minimized.
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>Makes the tray icon visible. Call once after construction.</summary>
    void Initialize();

    /// <summary>Updates the tray icon, tooltip, and menu text to reflect the running state.</summary>
    void UpdateState(bool isRunning);

    /// <summary>Shows a transient balloon notification near the tray icon.</summary>
    void ShowBalloon(string title, string message);

    /// <summary>Raised when the user asks to show/restore the main window.</summary>
    event EventHandler? OpenRequested;

    /// <summary>Raised when the user chooses "Start Jiggling" from the tray menu.</summary>
    event EventHandler? StartRequested;

    /// <summary>Raised when the user chooses "Stop Jiggling" from the tray menu.</summary>
    event EventHandler? StopRequested;

    /// <summary>Raised when the user chooses "Exit" from the tray menu.</summary>
    event EventHandler? ExitRequested;
}
