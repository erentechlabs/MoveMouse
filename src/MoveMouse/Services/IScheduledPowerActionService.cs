using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>
/// Tracks the configured scheduled power action (shut down or sleep) and
/// raises events as the computed run time approaches and arrives. This
/// service only owns timing/scheduling logic: it does not stop the mouse
/// jiggler and does not perform the actual shutdown/sleep itself. The
/// view model listens to its events, stops jiggling first, and then invokes
/// <see cref="IPowerActionExecutor"/> &#8212; matching the same
/// service/orchestrator split already used for the jiggler and power
/// management services.
/// </summary>
public interface IScheduledPowerActionService : IDisposable
{
    /// <summary>True while a schedule is configured and active.</summary>
    bool IsEnabled { get; }

    /// <summary>The next computed run time, or null while disabled.</summary>
    DateTimeOffset? NextRunAt { get; }

    /// <summary>True while the pre-action warning countdown is actively running.</summary>
    bool IsWarningActive { get; }

    /// <summary>Remaining time in the warning countdown; <see cref="TimeSpan.Zero"/> when not active.</summary>
    TimeSpan WarningRemaining { get; }

    /// <summary>
    /// Updates the schedule configuration. Safe to call at any time. Changing
    /// <paramref name="triggerMode"/> or <paramref name="countdownMinutes"/>
    /// while in "after a countdown" mode restarts that countdown from now.
    /// </summary>
    void Configure(
        bool enabled,
        ScheduledActionType actionType,
        ScheduledActionTriggerMode triggerMode,
        int timeOfDayMinutes,
        int countdownMinutes);

    /// <summary>
    /// Cancels an in-progress warning countdown without performing the
    /// action. For "at a specific time" mode this only skips today's run
    /// (the schedule still fires tomorrow); for "after a countdown" mode
    /// &#8212; a one-shot schedule &#8212; this turns the schedule off entirely.
    /// </summary>
    void CancelPendingAction();

    /// <summary>Raised roughly once per second while enabled, so the UI can refresh its status/countdown text.</summary>
    event EventHandler? Tick;

    /// <summary>Raised once when the pre-action warning period begins.</summary>
    event EventHandler<ScheduledActionType>? WarningStarted;

    /// <summary>Raised when an active warning is called off via <see cref="CancelPendingAction"/>.</summary>
    event EventHandler? WarningCancelled;

    /// <summary>Raised when the countdown reaches zero: the action should be performed now.</summary>
    event EventHandler<ScheduledActionType>? ActionDue;

    /// <summary>Raised when a one-shot ("after a countdown") schedule turns itself off after running or being cancelled.</summary>
    event EventHandler? ScheduleAutoDisabled;
}
