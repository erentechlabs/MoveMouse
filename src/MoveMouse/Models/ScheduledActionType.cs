namespace MoveMouse.Models;

/// <summary>The power action Move Mouse performs when a scheduled task runs.</summary>
public enum ScheduledActionType
{
    /// <summary>Put the PC to sleep (suspend to RAM). Wakes normally on user input.</summary>
    Sleep,

    /// <summary>Shut the PC down completely.</summary>
    Shutdown
}
