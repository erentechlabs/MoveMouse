using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MoveMouse.Services;
using MoveMouse.Tray;
using MoveMouse.ViewModels;

namespace MoveMouse;

/// <summary>
/// Application entry point and composition root. Constructs the services
/// and <see cref="MainViewModel"/>, owns the single <see cref="MainWindow"/>,
/// and guarantees sleep-prevention state is restored on the way out.
/// </summary>
public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ISettingsService settingsService = new SettingsService();
        IMouseJigglerService jiggler = new MouseJigglerService();
        IPowerManagementService power = new PowerManagementService();
        IStartupService startup = new StartupService();
        ITrayIconService tray = new TrayIconService();
        IScheduledPowerActionService scheduledAction = new ScheduledPowerActionService();
        IPowerActionExecutor powerActionExecutor = new PowerActionExecutor();

        _viewModel = new MainViewModel(jiggler, power, settingsService, startup, tray, scheduledAction, powerActionExecutor, dispatcherQueue);
        _viewModel.ExitRequested += OnExitRequested;

        _mainWindow = new MainWindow(_viewModel);
        _mainWindow.Activate();

        if (_viewModel.StartMinimizedToTray)
        {
            _mainWindow.HideToTray();
        }

        _viewModel.ApplyAutoStart();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _viewModel?.PrepareForExit();
        _mainWindow?.ForceClose();
        Exit();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Never let an unexpected exception leave the system stuck awake, and
        // never let it silently vanish either: log it and keep running if at
        // all possible instead of crashing.
        e.Handled = true;

        try
        {
            _viewModel?.PrepareForExit();
        }
        catch
        {
            // Best-effort cleanup only; nothing else we can safely do here.
        }

        TryLogFatal(e.Exception, e.Message);
    }

    private static void TryLogFatal(Exception? exception, string message)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MoveMouse");
            Directory.CreateDirectory(folder);

            var logPath = Path.Combine(folder, "error.log");
            var entry = $"[{DateTimeOffset.Now:u}] {message}{Environment.NewLine}{exception}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // If we can't even write the log, there is nothing further to do.
        }
    }
}
