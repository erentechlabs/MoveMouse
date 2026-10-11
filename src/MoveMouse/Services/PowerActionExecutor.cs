using System.Diagnostics;
using MoveMouse.Interop;
using MoveMouse.Models;

namespace MoveMouse.Services;

/// <summary>
/// <see cref="IPowerActionExecutor"/> implementation that actually shuts down
/// or suspends the machine. Shutdown is requested via the built-in
/// <c>shutdown.exe</c> tool (the simplest reliable way to request a clean
/// shutdown without manually acquiring <c>SE_SHUTDOWN_NAME</c> privilege).
/// Sleep is requested directly via the Win32 <c>SetSuspendState</c> API.
/// </summary>
public sealed class PowerActionExecutor : IPowerActionExecutor
{
    public bool Execute(ScheduledActionType action) => action switch
    {
        ScheduledActionType.Shutdown => Shutdown(),
        ScheduledActionType.Sleep => Sleep(),
        _ => false
    };

    private static bool Shutdown()
    {
        try
        {
            // "/s" = shut down, "/t 0" = no additional OS-level delay (Move
            // Mouse has already shown its own warning countdown). No "/f" so
            // Windows still gives other applications the normal chance to
            // prompt the user to save unsaved work, exactly like a manual
            // shutdown from the Start menu.
            using var process = Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });

            return process is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool Sleep()
    {
        try
        {
            // hibernate=false (sleep, not hibernate), forceCritical=false (let
            // other applications veto/prompt as they would for a manual
            // Start menu "Sleep"), disableWakeEvent=false (keep normal wake
            // sources such as the keyboard/mouse working).
            return NativeMethods.SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
