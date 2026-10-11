using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MoveMouse.ViewModels;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace MoveMouse;

/// <summary>
/// The single dashboard window. Hosts the <see cref="MainViewModel"/> as its
/// DataContext and owns window-chrome concerns: sizing, icon, and
/// intercepting the close button to minimize to the tray instead of exiting
/// when the user has that preference enabled.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly AppWindow? _appWindow;
    private bool _isExiting;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();
        Title = "Move Mouse";
        RootGrid.DataContext = ViewModel;

        try
        {
            // Falls back gracefully on systems/configurations that do not support Mica.
            SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // Purely cosmetic; never let this prevent the window from showing.
        }

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            try
            {
                _appWindow.SetIcon("Assets/AppIcon.ico");
            }
            catch
            {
                // Missing/unreadable icon file should never prevent the window from showing.
            }

            _appWindow.Resize(GetPhysicalSize(hwnd, 480, 780));
            _appWindow.Closing += OnAppWindowClosing;

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
            }
        }

        ViewModel.ShowWindowRequested += (_, _) => RestoreAndActivate();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
        {
            return;
        }

        if (ViewModel.MinimizeToTrayOnClose)
        {
            args.Cancel = true;
            HideToTray();
        }
    }

    /// <summary>Hides the window to the notification area without ending the process.</summary>
    public void HideToTray() => _appWindow?.Hide();

    /// <summary>Shows and focuses the window, e.g. in response to a tray icon click.</summary>
    public void RestoreAndActivate()
    {
        _appWindow?.Show();
        Activate();
    }

    /// <summary>Bypasses the minimize-to-tray interception so the window actually closes.</summary>
    public void ForceClose()
    {
        _isExiting = true;
        Close();
    }

    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(IntPtr hwnd);

    /// <summary>
    /// Converts a size expressed in DIPs (the same unit XAML Width/Margin/etc. use) to the
    /// physical pixels <see cref="AppWindow.Resize"/> actually expects. Without this
    /// conversion, on any display scaled above 100% the window — and therefore the
    /// available layout space for its XAML content — ends up smaller than intended,
    /// which is what previously caused settings rows to wrap/clip incorrectly.
    /// </summary>
    private static SizeInt32 GetPhysicalSize(IntPtr hwnd, int widthInDips, int heightInDips)
    {
        var scale = GetDpiForWindow(hwnd) / 96.0;
        return new SizeInt32((int)(widthInDips * scale), (int)(heightInDips * scale));
    }
}
