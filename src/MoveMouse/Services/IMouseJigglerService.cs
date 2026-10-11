using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>
/// Periodically simulates light mouse activity so Windows treats the
/// machine as "in use". Movement timing and pattern are configured via
/// <see cref="Configure"/> and can be changed at any time, whether the
/// jiggler is running or stopped.
/// </summary>
public interface IMouseJigglerService : IDisposable
{
    /// <summary>True while the jiggler is actively moving the cursor on a timer.</summary>
    bool IsRunning { get; }

    /// <summary>Timestamp of the most recent simulated cursor movement, if any.</summary>
    DateTimeOffset? LastMovedAt { get; }

    /// <summary>Raised whenever <see cref="IsRunning"/> changes.</summary>
    event EventHandler<bool>? RunningStateChanged;

    /// <summary>Raised on the background timer thread immediately after a successful cursor move.</summary>
    event EventHandler<DateTimeOffset>? CursorMoved;

    /// <summary>Raised when a movement tick fails unexpectedly; carries a user-facing message.</summary>
    event EventHandler<string>? ErrorOccurred;

    /// <summary>Updates the interval, movement pattern, and distance used on every tick.</summary>
    void Configure(int intervalSeconds, MovementMode mode, int distancePixels);

    /// <summary>Starts (or resumes) periodic cursor movement. No-op if already running.</summary>
    void Start();

    /// <summary>Stops periodic cursor movement. No-op if already stopped.</summary>
    void Stop();
}
