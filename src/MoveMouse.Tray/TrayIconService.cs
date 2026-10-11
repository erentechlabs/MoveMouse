using System.Drawing;
using System.Windows.Forms;

namespace MoveMouse.Tray;

/// <summary>
/// <see cref="ITrayIconService"/> implementation built on
/// <see cref="System.Windows.Forms.NotifyIcon"/>. WinUI 3 has no built-in
/// notification-area API, so this service deliberately opts in to Windows
/// Forms (via <c>UseWindowsForms</c> in this project's own file, kept
/// separate from the WinUI 3 UI project) purely for the tray icon and its
/// context menu.
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly Icon _idleIcon;
    private readonly Icon _activeIcon;
    private bool _isRunning;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? StartRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        _idleIcon = new Icon(Path.Combine(assetsDir, "TrayIdle.ico"));
        _activeIcon = new Icon(Path.Combine(assetsDir, "TrayActive.ico"));

        var openItem = new ToolStripMenuItem("Open Move Mouse");
        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _toggleMenuItem = new ToolStripMenuItem("Start Jiggling");
        _toggleMenuItem.Click += (_, _) =>
        {
            if (_isRunning)
            {
                StopRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StartRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _idleIcon,
            Text = "Move Mouse — Stopped",
            ContextMenuStrip = menu,
            Visible = false
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public void Initialize() => _notifyIcon.Visible = true;

    public void UpdateState(bool isRunning)
    {
        _isRunning = isRunning;
        _notifyIcon.Icon = isRunning ? _activeIcon : _idleIcon;
        _notifyIcon.Text = isRunning ? "Move Mouse — Running" : "Move Mouse — Stopped";
        _toggleMenuItem.Text = isRunning ? "Stop Jiggling" : "Start Jiggling";
    }

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _idleIcon.Dispose();
        _activeIcon.Dispose();
        _disposed = true;
    }
}
