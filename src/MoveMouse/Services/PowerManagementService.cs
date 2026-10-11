using MoveMouse.Interop;

namespace MoveMouse.Services;

/// <summary>
/// <see cref="IPowerManagementService"/> implementation backed by the Win32
/// <c>SetThreadExecutionState</c> API.
/// </summary>
/// <remarks>
/// <c>SetThreadExecutionState</c> applies to the calling thread only, and
/// Windows automatically clears the flag if that thread ever exits. Callers
/// must therefore always invoke this service from the same long-lived
/// thread &#8212; in this application, the UI thread, which lives for the
/// entire process lifetime. A single call with <c>ES_CONTINUOUS</c> persists
/// the requested state until explicitly changed, so there is no need to poll
/// or re-issue the call on a timer.
/// </remarks>
public sealed class PowerManagementService : IPowerManagementService
{
    public bool IsPreventingSleep { get; private set; }

    public bool PreventSleep(bool keepDisplayOn)
    {
        try
        {
            var flags = NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED;
            if (keepDisplayOn)
            {
                flags |= NativeMethods.ES_DISPLAY_REQUIRED;
            }

            var result = NativeMethods.SetThreadExecutionState(flags);
            IsPreventingSleep = result != 0;
            return IsPreventingSleep;
        }
        catch (Exception)
        {
            // Treat any native interop failure as "could not engage sleep prevention"
            // rather than crashing the application.
            IsPreventingSleep = false;
            return false;
        }
    }

    public bool AllowSleep()
    {
        try
        {
            var result = NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            IsPreventingSleep = false;
            return result != 0;
        }
        catch (Exception)
        {
            IsPreventingSleep = false;
            return false;
        }
    }
}
