using System.Threading;
using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>
/// <see cref="IScheduledPowerActionService"/> implementation driven by a
/// one-second <see cref="System.Threading.Timer"/>, following the same
/// lock-guarded-state pattern as <see cref="MouseJigglerService"/> so
/// <see cref="Configure"/> can safely be called from the UI thread while a
/// tick callback is running on a thread-pool thread.
/// </summary>
public sealed class ScheduledPowerActionService : IScheduledPowerActionService
{
    private static readonly TimeSpan WarningPeriod = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private Timer? _timer;
    private bool _disposed;

    private bool _enabled;
    private ScheduledActionType _actionType = ScheduledActionType.Sleep;
    private ScheduledActionTriggerMode _triggerMode = ScheduledActionTriggerMode.AtSpecificTime;
    private int _timeOfDayMinutes = 22 * 60;
    private int _countdownMinutes = 60;

    private DateTimeOffset? _countdownStartedAt;
    private DateOnly? _lastFiredDateAtSpecificTime;
    private bool _warningActive;

    public bool IsEnabled => _enabled;

    public DateTimeOffset? NextRunAt { get; private set; }

    public bool IsWarningActive => _warningActive;

    public TimeSpan WarningRemaining { get; private set; }

    public event EventHandler? Tick;
    public event EventHandler<ScheduledActionType>? WarningStarted;
    public event EventHandler? WarningCancelled;
    public event EventHandler<ScheduledActionType>? ActionDue;
    public event EventHandler? ScheduleAutoDisabled;

    public void Configure(
        bool enabled,
        ScheduledActionType actionType,
        ScheduledActionTriggerMode triggerMode,
        int timeOfDayMinutes,
        int countdownMinutes)
    {
        if (timeOfDayMinutes is < 0 or >= 24 * 60)
        {
            throw new ArgumentOutOfRangeException(nameof(timeOfDayMinutes), "Time of day must be between 0 and 1439 minutes.");
        }

        if (countdownMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(countdownMinutes), "Countdown must be at least 1 minute.");
        }

        lock (_gate)
        {
            var wasEnabled = _enabled;
            var previousTriggerMode = _triggerMode;
            var previousCountdownMinutes = _countdownMinutes;

            _enabled = enabled;
            _actionType = actionType;
            _triggerMode = triggerMode;
            _timeOfDayMinutes = timeOfDayMinutes;
            _countdownMinutes = countdownMinutes;

            if (!enabled)
            {
                _warningActive = false;
                WarningRemaining = TimeSpan.Zero;
                _countdownStartedAt = null;
                NextRunAt = null;
                StopTimerLocked();
                return;
            }

            if (triggerMode == ScheduledActionTriggerMode.AfterCountdown)
            {
                var needsFreshStart = !wasEnabled
                    || previousTriggerMode != ScheduledActionTriggerMode.AfterCountdown
                    || previousCountdownMinutes != countdownMinutes;

                if (needsFreshStart)
                {
                    _countdownStartedAt = DateTimeOffset.Now;
                    _warningActive = false;
                    WarningRemaining = TimeSpan.Zero;
                }
            }
            else if (!wasEnabled || previousTriggerMode != ScheduledActionTriggerMode.AtSpecificTime)
            {
                _lastFiredDateAtSpecificTime = null;
                _warningActive = false;
                WarningRemaining = TimeSpan.Zero;
            }

            EnsureTimerRunningLocked();
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    public void CancelPendingAction()
    {
        var wasWarning = false;
        var autoDisabled = false;

        lock (_gate)
        {
            if (!_warningActive)
            {
                return;
            }

            wasWarning = true;
            _warningActive = false;
            WarningRemaining = TimeSpan.Zero;

            if (_triggerMode == ScheduledActionTriggerMode.AtSpecificTime)
            {
                // Skip just today's run; the schedule stays on for tomorrow.
                _lastFiredDateAtSpecificTime = DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime);
            }
            else
            {
                // One-shot schedule: cancelling the only planned run turns it off.
                _enabled = false;
                NextRunAt = null;
                StopTimerLocked();
                autoDisabled = true;
            }
        }

        if (wasWarning)
        {
            WarningCancelled?.Invoke(this, EventArgs.Empty);
        }

        if (autoDisabled)
        {
            ScheduleAutoDisabled?.Invoke(this, EventArgs.Empty);
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureTimerRunningLocked()
    {
        if (_disposed)
        {
            return;
        }

        _timer ??= new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void StopTimerLocked()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTick(object? state)
    {
        ScheduledActionType actionType;
        ScheduledActionTriggerMode triggerMode;
        var enteringWarning = false;
        var firing = false;
        var autoDisabled = false;

        lock (_gate)
        {
            if (!_enabled || _disposed)
            {
                return;
            }

            actionType = _actionType;
            triggerMode = _triggerMode;

            var now = DateTimeOffset.Now;
            var target = ComputeTargetLocked(now);
            NextRunAt = target;

            var remaining = target - now;

            if (remaining <= TimeSpan.Zero)
            {
                firing = true;
                _warningActive = false;
                WarningRemaining = TimeSpan.Zero;

                if (triggerMode == ScheduledActionTriggerMode.AtSpecificTime)
                {
                    _lastFiredDateAtSpecificTime = DateOnly.FromDateTime(now.LocalDateTime);
                }
                else
                {
                    _enabled = false;
                    StopTimerLocked();
                    autoDisabled = true;
                }
            }
            else if (remaining <= WarningPeriod)
            {
                if (!_warningActive)
                {
                    _warningActive = true;
                    enteringWarning = true;
                }

                WarningRemaining = remaining;
            }
            else if (_warningActive)
            {
                // Target moved back out past the warning window (e.g. settings
                // changed mid-warning); clear the stale warning state.
                _warningActive = false;
                WarningRemaining = TimeSpan.Zero;
            }
        }

        if (enteringWarning)
        {
            WarningStarted?.Invoke(this, actionType);
        }

        if (firing)
        {
            ActionDue?.Invoke(this, actionType);

            if (autoDisabled)
            {
                ScheduleAutoDisabled?.Invoke(this, EventArgs.Empty);
            }
        }

        Tick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Computes the next run time given the current configuration. Must be
    /// called while holding <see cref="_gate"/>.
    /// </summary>
    private DateTimeOffset ComputeTargetLocked(DateTimeOffset now)
    {
        if (_triggerMode == ScheduledActionTriggerMode.AfterCountdown)
        {
            var start = _countdownStartedAt ?? now;
            return start + TimeSpan.FromMinutes(_countdownMinutes);
        }

        var todayTarget = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset)
            + TimeSpan.FromMinutes(_timeOfDayMinutes);

        var today = DateOnly.FromDateTime(now.LocalDateTime);
        if (_lastFiredDateAtSpecificTime == today)
        {
            // Already ran (or was skipped via cancel) today; next occurrence is tomorrow.
            return todayTarget.AddDays(1);
        }

        return todayTarget;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopTimerLocked();
        }
    }
}
