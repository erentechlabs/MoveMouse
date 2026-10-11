namespace MoveMouse.Services;

/// <summary>Registers (or unregisters) Move Mouse to launch automatically at Windows sign-in.</summary>
public interface IStartupService
{
    /// <summary>True if Move Mouse is currently registered to launch at sign-in.</summary>
    bool IsRegistered();

    /// <summary>Adds or removes the Windows startup registration.</summary>
    /// <returns>True if the operation succeeded.</returns>
    bool SetRegistered(bool enabled);
}
