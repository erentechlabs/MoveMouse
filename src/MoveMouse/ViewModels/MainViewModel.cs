using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using MoveMouse.Models;
using MoveMouse.Services;
using MoveMouse.Tray;

namespace MoveMouse.ViewModels;

/// <summary>
/// Backs the main dashboard. Owns no Windows API calls directly; instead it
/// coordinates <see cref="IMouseJigglerService"/>, <see cref="IPowerManagementService"/>,
/// <see cref="ISettingsService"/>, <see cref="IStartupService"/>, and
/// <see cref="ITrayIconService"/>, translating user intent (bound
/// properties/commands) into calls on those services and translating
/// service events back into UI state on the dispatcher thread.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IMouseJigglerService _jiggler;
    private readonly IPowerManagementService _power;
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly ITrayIconService _trayIconService;
    private readonly IScheduledPowerActionService _scheduledAction;
    private readonly IPowerActionExecutor _powerActionExecutor;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly AppSettings _settings;

    /// <summary>Which action the currently-active warning countdown (if any) is for.</summary>
    private ScheduledActionType _pendingWarningActionType;

    /// <summary>Guards against persisting/reconfiguring while initial values are being loaded from disk.</summary>
    private bool _isLoadingSettings = true;

    public ObservableCollection<MovementModeOption> MovementModes { get; } = new()
    {
        new MovementModeOption(MovementMode.RandomMovement, "Random movement"),
        new MovementModeOption(MovementMode.BackAndForth, "Back and forth"),
        new MovementModeOption(MovementMode.CircularNudge, "Circular nudge"),
    };

    public ObservableCollection<ScheduledActionTypeOption> ScheduledActionTypes { get; } = new()
    {
        new ScheduledActionTypeOption(ScheduledActionType.Sleep, "Sleep"),
        new ScheduledActionTypeOption(ScheduledActionType.Shutdown, "Shut down"),
    };

    public ObservableCollection<ScheduledActionTriggerModeOption> ScheduledActionTriggerModes { get; } = new()
    {
        new ScheduledActionTriggerModeOption(ScheduledActionTriggerMode.AtSpecificTime, "At a specific time"),
        new ScheduledActionTriggerModeOption(ScheduledActionTriggerMode.AfterCountdown, "After a countdown"),
    };

    [NotifyPropertyChangedFor(nameof(ToggleButtonText))]
    [NotifyPropertyChangedFor(nameof(ToggleButtonGlyph))]
    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    /// <summary>Label for the single Start/Stop toggle button, reflecting the current state.</summary>
    public string ToggleButtonText => IsRunning ? "Stop Jiggling" : "Start Jiggling";

    /// <summary>Segoe MDL2 Assets glyph for the toggle button (stop square / play triangle).</summary>
    public string ToggleButtonGlyph => IsRunning ? "\uE71A" : "\uE768";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Stopped";

    [ObservableProperty]
    public partial string StatusDetailText { get; set; } = "Click Start to keep your PC awake.";

    [ObservableProperty]
    public partial string LastMovedText { get; set; } = "No movement yet";

    [ObservableProperty]
    public partial int IntervalSeconds { get; set; } = 30;

    [ObservableProperty]
    public partial int MovementDistance { get; set; } = 6;

    [ObservableProperty]
    public partial MovementModeOption SelectedMovementMode { get; set; }

    [ObservableProperty]
    public partial bool KeepDisplayOn { get; set; } = true;

    [ObservableProperty]
    public partial bool MinimizeToTrayOnClose { get; set; } = true;

    [ObservableProperty]
    public partial bool StartMinimizedToTray { get; set; }

    [ObservableProperty]
    public partial bool AutoStartJigglingOnLaunch { get; set; }

    [ObservableProperty]
    public partial bool LaunchAtWindowsStartup { get; set; }

    [ObservableProperty]
    public partial bool ScheduledActionEnabled { get; set; }

    [ObservableProperty]
    public partial ScheduledActionTypeOption SelectedScheduledActionType { get; set; }

    [NotifyPropertyChangedFor(nameof(IsAtSpecificTimeMode))]
    [NotifyPropertyChangedFor(nameof(IsAfterCountdownMode))]
    [ObservableProperty]
    public partial ScheduledActionTriggerModeOption SelectedScheduledActionTriggerMode { get; set; }

    /// <summary>The time of day the schedule runs, bound directly to a WinUI <c>TimePicker</c>.</summary>
    [ObservableProperty]
    public partial TimeSpan ScheduledActionTime { get; set; } = new(22, 0, 0);

    [ObservableProperty]
    public partial int ScheduledActionCountdownMinutes { get; set; } = 60;

    /// <summary>True while the "At a specific time" row (the TimePicker) should be shown.</summary>
    public bool IsAtSpecificTimeMode => SelectedScheduledActionTriggerMode?.Value == ScheduledActionTriggerMode.AtSpecificTime;

    /// <summary>True while the "After a countdown" row (the countdown NumberBox) should be shown.</summary>
    public bool IsAfterCountdownMode => SelectedScheduledActionTriggerMode?.Value == ScheduledActionTriggerMode.AfterCountdown;

    /// <summary>Human-readable summary of the next scheduled run, or that none is scheduled.</summary>
    [ObservableProperty]
    public partial string ScheduleStatusText { get; set; } = "No scheduled shutdown or sleep.";

    /// <summary>True while the pre-action warning banner (with its Cancel button) should be shown.</summary>
    [ObservableProperty]
    public partial bool IsScheduleWarningActive { get; set; }

    [ObservableProperty]
    public partial string ScheduleWarningText { get; set; } = string.Empty;

    [RelayCommand]
    private void CancelScheduledAction()
    {
        _scheduledAction.CancelPendingAction();
        IsScheduleWarningActive = false;
    }

    [NotifyPropertyChangedFor(nameof(HasError))]
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>True when <see cref="ErrorMessage"/> has content, for binding an InfoBar's IsOpen.</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>True while the "Movement" settings tab is the active one.</summary>
    [ObservableProperty]
    public partial bool IsMovementTabActive { get; set; } = true;

    /// <summary>True while the "Behavior" settings tab is the active one.</summary>
    [ObservableProperty]
    public partial bool IsBehaviorTabActive { get; set; }

    /// <summary>True while the "Schedule" settings tab is the active one.</summary>
    [ObservableProperty]
    public partial bool IsScheduleTabActive { get; set; }

    [RelayCommand]
    private void ShowMovementTab()
    {
        IsMovementTabActive = true;
        IsBehaviorTabActive = false;
        IsScheduleTabActive = false;
    }

    [RelayCommand]
    private void ShowBehaviorTab()
    {
        IsMovementTabActive = false;
        IsBehaviorTabActive = true;
        IsScheduleTabActive = false;
    }

    [RelayCommand]
    private void ShowScheduleTab()
    {
        IsMovementTabActive = false;
        IsBehaviorTabActive = false;
        IsScheduleTabActive = true;
    }

    /// <summary>Raised when the user chooses Exit from the window or tray menu.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Raised when the tray icon is clicked/double-clicked to restore the window.</summary>
    public event EventHandler? ShowWindowRequested;

    public MainViewModel(
        IMouseJigglerService jiggler,
        IPowerManagementService power,
        ISettingsService settingsService,
        IStartupService startupService,
        ITrayIconService trayIconService,
        IScheduledPowerActionService scheduledAction,
        IPowerActionExecutor powerActionExecutor,
        DispatcherQueue dispatcherQueue)
    {
        _jiggler = jiggler;
        _power = power;
        _settingsService = settingsService;
        _startupService = startupService;
        _trayIconService = trayIconService;
        _scheduledAction = scheduledAction;
        _powerActionExecutor = powerActionExecutor;
        _dispatcherQueue = dispatcherQueue;

        _settings = _settingsService.Load();

        _isLoadingSettings = true;
        IntervalSeconds = Math.Clamp(_settings.IntervalSeconds, 1, 3600);
        MovementDistance = Math.Clamp(_settings.MovementDistancePixels, 1, 100);
        SelectedMovementMode = MovementModes.FirstOrDefault(m => m.Value == _settings.MovementMode) ?? MovementModes[0];
        KeepDisplayOn = _settings.KeepDisplayOn;
        MinimizeToTrayOnClose = _settings.MinimizeToTrayOnClose;
        StartMinimizedToTray = _settings.StartMinimizedToTray;
        AutoStartJigglingOnLaunch = _settings.AutoStartJigglingOnLaunch;
        LaunchAtWindowsStartup = _settings.LaunchAtWindowsStartup;
        ScheduledActionEnabled = _settings.ScheduledActionEnabled;
        SelectedScheduledActionType = ScheduledActionTypes.FirstOrDefault(t => t.Value == _settings.ScheduledActionType) ?? ScheduledActionTypes[0];
        SelectedScheduledActionTriggerMode = ScheduledActionTriggerModes.FirstOrDefault(t => t.Value == _settings.ScheduledActionTriggerMode) ?? ScheduledActionTriggerModes[0];
        var clampedTimeOfDayMinutes = Math.Clamp(_settings.ScheduledActionTimeOfDayMinutes, 0, 24 * 60 - 1);
        ScheduledActionTime = TimeSpan.FromMinutes(clampedTimeOfDayMinutes);
        ScheduledActionCountdownMinutes = Math.Clamp(_settings.ScheduledActionCountdownMinutes, 1, 1440);
        _isLoadingSettings = false;

        _jiggler.Configure(IntervalSeconds, SelectedMovementMode.Value, MovementDistance);
        _jiggler.RunningStateChanged += OnJigglerRunningStateChanged;
        _jiggler.CursorMoved += OnJigglerCursorMoved;
        _jiggler.ErrorOccurred += OnJigglerError;

        _scheduledAction.Configure(
            ScheduledActionEnabled,
            SelectedScheduledActionType.Value,
            SelectedScheduledActionTriggerMode.Value,
            clampedTimeOfDayMinutes,
            ScheduledActionCountdownMinutes);
        _scheduledAction.Tick += OnScheduleTick;
        _scheduledAction.WarningStarted += OnScheduleWarningStarted;
        _scheduledAction.WarningCancelled += OnScheduleWarningCancelled;
        _scheduledAction.ActionDue += OnScheduleActionDue;
        _scheduledAction.ScheduleAutoDisabled += OnScheduleAutoDisabled;
        ScheduleStatusText = BuildScheduleStatusText();

        _trayIconService.OpenRequested += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        _trayIconService.StartRequested += (_, _) => Start();
        _trayIconService.StopRequested += (_, _) => Stop();
        _trayIconService.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _trayIconService.Initialize();
    }

    /// <summary>Applies <see cref="AppSettings.AutoStartJigglingOnLaunch"/>. Called once after the window is ready.</summary>
    public void ApplyAutoStart()
    {
        if (AutoStartJigglingOnLaunch && !IsRunning)
        {
            Start();
        }
    }

    /// <summary>Toggles between jiggling and stopped. Bound to the single Start/Stop button and the tray menu.</summary>
    [RelayCommand]
    private void ToggleJiggling()
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    private void Start()
    {
        ErrorMessage = null;
        _jiggler.Start();

        if (!_power.PreventSleep(KeepDisplayOn))
        {
            ErrorMessage = "Windows did not confirm the sleep-prevention request. Mouse movement is still active, but the PC may still sleep.";
        }
    }

    private void Stop()
    {
        _jiggler.Stop();
        if (!_power.AllowSleep())
        {
            ErrorMessage = "Windows did not confirm the restore-power-management request.";
        }
    }

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    private void OnJigglerRunningStateChanged(object? sender, bool running)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsRunning = running;
            StatusText = running ? "Running" : "Stopped";
            StatusDetailText = running
                ? $"Nudging the cursor every {IntervalSeconds}s to keep your PC awake."
                : "Click Start to keep your PC awake.";
            _trayIconService.UpdateState(running);
        });
    }

    private void OnJigglerCursorMoved(object? sender, DateTimeOffset at)
    {
        _dispatcherQueue.TryEnqueue(() => LastMovedText = $"Last moved at {at:T}");
    }

    private void OnJigglerError(object? sender, string message)
    {
        _dispatcherQueue.TryEnqueue(() => ErrorMessage = message);
    }

    private void OnScheduleTick(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            ScheduleStatusText = BuildScheduleStatusText();
            if (_scheduledAction.IsWarningActive)
            {
                ScheduleWarningText = BuildScheduleWarningText();
            }
        });
    }

    private void OnScheduleWarningStarted(object? sender, ScheduledActionType actionType)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _pendingWarningActionType = actionType;
            IsScheduleWarningActive = true;
            ScheduleWarningText = BuildScheduleWarningText();

            // Stop jiggling (and restore normal power management) before the
            // scheduled action runs, per the feature's whole purpose: never
            // let the jiggler's own activity fight the shutdown/sleep it is
            // about to trigger.
            Stop();

            _trayIconService.ShowBalloon("Move Mouse", ScheduleWarningText);
        });
    }

    private void OnScheduleWarningCancelled(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() => IsScheduleWarningActive = false);
    }

    private void OnScheduleActionDue(object? sender, ScheduledActionType actionType)
    {
        _dispatcherQueue.TryEnqueue(() => ExecuteScheduledAction(actionType));
    }

    private void OnScheduleAutoDisabled(object? sender, EventArgs e)
    {
        // The service already turned its own internal "enabled" flag off
        // (this only happens for the one-shot "after a countdown" mode, once
        // it has fired or been cancelled); mirror that back into the bound
        // toggle and persisted settings.
        _dispatcherQueue.TryEnqueue(() => ScheduledActionEnabled = false);
    }

    /// <summary>Stops jiggling and performs the scheduled power action. Runs on the UI thread.</summary>
    private void ExecuteScheduledAction(ScheduledActionType actionType)
    {
        IsScheduleWarningActive = false;
        Stop();
        SaveSettings();

        if (!_powerActionExecutor.Execute(actionType))
        {
            ErrorMessage = actionType == ScheduledActionType.Shutdown
                ? "Could not start the scheduled shutdown. You can shut down manually from the Start menu."
                : "Could not put the PC to sleep for the scheduled task. You can sleep it manually from the Start menu.";
        }
    }

    private string BuildScheduleStatusText()
    {
        if (!ScheduledActionEnabled || _scheduledAction.NextRunAt is not { } nextRun)
        {
            return "No scheduled shutdown or sleep.";
        }

        var actionWord = SelectedScheduledActionType.Value == ScheduledActionType.Shutdown ? "Shut down" : "Sleep";
        var now = DateTimeOffset.Now;
        var remaining = nextRun - now;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        var remainingText = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m"
            : $"{Math.Max(1, remaining.Minutes)}m";

        if (SelectedScheduledActionTriggerMode.Value == ScheduledActionTriggerMode.AfterCountdown)
        {
            return $"{actionWord} scheduled in {remainingText} (around {nextRun:t}).";
        }

        var dayWord = nextRun.Date == now.Date
            ? "today"
            : nextRun.Date == now.Date.AddDays(1)
                ? "tomorrow"
                : nextRun.ToString("MMM d");

        return $"{actionWord} scheduled for {dayWord} at {nextRun:t} (in {remainingText}).";
    }

    private string BuildScheduleWarningText()
    {
        var actionWord = _pendingWarningActionType == ScheduledActionType.Shutdown ? "Shutting down" : "Going to sleep";
        var seconds = Math.Max(0, (int)Math.Ceiling(_scheduledAction.WarningRemaining.TotalSeconds));
        return $"{actionWord} in {seconds}s to run your scheduled task. Move Mouse has stopped jiggling.";
    }

    partial void OnIntervalSecondsChanged(int value) => ApplyJigglerConfigurationAndSave();

    partial void OnMovementDistanceChanged(int value) => ApplyJigglerConfigurationAndSave();

    partial void OnSelectedMovementModeChanged(MovementModeOption value) => ApplyJigglerConfigurationAndSave();

    partial void OnKeepDisplayOnChanged(bool value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        if (IsRunning)
        {
            _power.PreventSleep(value);
        }

        SaveSettings();
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value) => SaveSettings();

    partial void OnStartMinimizedToTrayChanged(bool value) => SaveSettings();

    partial void OnAutoStartJigglingOnLaunchChanged(bool value) => SaveSettings();

    partial void OnLaunchAtWindowsStartupChanged(bool value)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        if (!_startupService.SetRegistered(value))
        {
            ErrorMessage = "Could not update the Windows startup registration. Try running Move Mouse as the current user (not elevated).";
        }

        SaveSettings();
    }

    partial void OnScheduledActionEnabledChanged(bool value) => ApplyScheduleConfigurationAndSave();

    partial void OnSelectedScheduledActionTypeChanged(ScheduledActionTypeOption value) => ApplyScheduleConfigurationAndSave();

    partial void OnSelectedScheduledActionTriggerModeChanged(ScheduledActionTriggerModeOption value) => ApplyScheduleConfigurationAndSave();

    partial void OnScheduledActionTimeChanged(TimeSpan value) => ApplyScheduleConfigurationAndSave();

    partial void OnScheduledActionCountdownMinutesChanged(int value) => ApplyScheduleConfigurationAndSave();

    private void ApplyScheduleConfigurationAndSave()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        try
        {
            var timeOfDayMinutes = (int)ScheduledActionTime.TotalMinutes;
            _scheduledAction.Configure(
                ScheduledActionEnabled,
                SelectedScheduledActionType.Value,
                SelectedScheduledActionTriggerMode.Value,
                timeOfDayMinutes,
                ScheduledActionCountdownMinutes);
            ScheduleStatusText = BuildScheduleStatusText();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ErrorMessage = ex.Message;
        }

        SaveSettings();
    }

    private void ApplyJigglerConfigurationAndSave()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        try
        {
            _jiggler.Configure(IntervalSeconds, SelectedMovementMode.Value, MovementDistance);
            if (IsRunning)
            {
                StatusDetailText = $"Nudging the cursor every {IntervalSeconds}s to keep your PC awake.";
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ErrorMessage = ex.Message;
        }

        SaveSettings();
    }

    private void SaveSettings()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _settings.IntervalSeconds = IntervalSeconds;
        _settings.MovementDistancePixels = MovementDistance;
        _settings.MovementMode = SelectedMovementMode.Value;
        _settings.KeepDisplayOn = KeepDisplayOn;
        _settings.MinimizeToTrayOnClose = MinimizeToTrayOnClose;
        _settings.StartMinimizedToTray = StartMinimizedToTray;
        _settings.AutoStartJigglingOnLaunch = AutoStartJigglingOnLaunch;
        _settings.LaunchAtWindowsStartup = LaunchAtWindowsStartup;
        _settings.ScheduledActionEnabled = ScheduledActionEnabled;
        _settings.ScheduledActionType = SelectedScheduledActionType.Value;
        _settings.ScheduledActionTriggerMode = SelectedScheduledActionTriggerMode.Value;
        _settings.ScheduledActionTimeOfDayMinutes = (int)ScheduledActionTime.TotalMinutes;
        _settings.ScheduledActionCountdownMinutes = ScheduledActionCountdownMinutes;
        _settingsService.Save(_settings);
    }

    /// <summary>Stops jiggling, restores power management, and flushes settings. Call once before process exit.</summary>
    public void PrepareForExit()
    {
        _jiggler.Stop();
        _power.AllowSleep();
        SaveSettings();
    }

    public void Dispose()
    {
        _jiggler.RunningStateChanged -= OnJigglerRunningStateChanged;
        _jiggler.CursorMoved -= OnJigglerCursorMoved;
        _jiggler.ErrorOccurred -= OnJigglerError;
        _jiggler.Dispose();

        _scheduledAction.Tick -= OnScheduleTick;
        _scheduledAction.WarningStarted -= OnScheduleWarningStarted;
        _scheduledAction.WarningCancelled -= OnScheduleWarningCancelled;
        _scheduledAction.ActionDue -= OnScheduleActionDue;
        _scheduledAction.ScheduleAutoDisabled -= OnScheduleAutoDisabled;
        _scheduledAction.Dispose();

        _trayIconService.Dispose();
    }
}
