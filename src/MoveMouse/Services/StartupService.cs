using Microsoft.Win32;

namespace MoveMouse.Services;

/// <summary>
/// <see cref="IStartupService"/> implementation using the per-user
/// <c>HKEY_CURRENT_USER\...\CurrentVersion\Run</c> registry key. This
/// requires no administrator privileges and affects only the current user.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MoveMouse";

    public bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string existing
                   && existing.Equals(GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool SetRegistered(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                key.SetValue(ValueName, GetExecutablePath());
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        // Quote the path defensively in case the install location contains spaces.
        var path = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "MoveMouse.exe");
        return $"\"{path}\"";
    }
}
