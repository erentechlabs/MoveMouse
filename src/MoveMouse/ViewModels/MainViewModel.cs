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
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly AppSettings _settings;

    /// <summary>Guards against persisting/reconfiguring while initial values are being loaded from disk.</summary>
    private bool _isLoadingSettings = true;

    public ObservableCollection<MovementModeOption> MovementModes { get; } = new()
    {
        new MovementModeOption(MovementMode.RandomMovement, "Random movement"),
        new MovementModeOption(MovementMode.BackAndForth, "Back and forth"),
        new MovementModeOption(MovementMode.CircularNudge, "Circular nudge"),
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

    [NotifyPropertyChangedFor(nameof(HasError))]
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>True when <see cref="ErrorMessage"/> has content, for binding an InfoBar's IsOpen.</summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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
        DispatcherQueue dispatcherQueue)
    {
        _jiggler = jiggler;
        _power = power;
        _settingsService = settingsService;
        _startupService = startupService;
        _trayIconService = trayIconService;
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
        _isLoadingSettings = false;

        _jiggler.Configure(IntervalSeconds, SelectedMovementMode.Value, MovementDistance);
        _jiggler.RunningStateChanged += OnJigglerRunningStateChanged;
        _jiggler.CursorMoved += OnJigglerCursorMoved;
        _jiggler.ErrorOccurred += OnJigglerError;

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
        _trayIconService.Dispose();
    }
}
