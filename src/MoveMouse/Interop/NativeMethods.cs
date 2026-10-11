using System.Runtime.InteropServices;

namespace MoveMouse.Interop;

/// <summary>
/// Thin wrappers around the native Win32 APIs Move Mouse relies on:
/// synthesizing mouse input (<see cref="SendInput"/>) and controlling the
/// system power/idle state (<see cref="SetThreadExecutionState"/>).
/// Keeping all P/Invoke signatures in one place makes the rest of the
/// codebase free of unsafe/native concerns.
/// </summary>
internal static class NativeMethods
{
    #region SendInput (mouse movement)

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    internal const uint INPUT_MOUSE = 0;
    internal const uint MOUSEEVENTF_MOVE = 0x0001;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    /// <summary>
    /// Moves the cursor by a relative offset using <c>SendInput</c>, which is
    /// the Microsoft-recommended replacement for the legacy <c>mouse_event</c>
    /// API and is recognized by Windows as genuine input for idle-timer
    /// purposes (required for sleep/screensaver prevention to be effective).
    /// </summary>
    internal static bool MoveCursorBy(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return true;
        }

        var inputs = new INPUT[1];
        inputs[0] = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = dx,
                dy = dy,
                mouseData = 0,
                dwFlags = MOUSEEVENTF_MOVE,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        var sent = SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        return sent == 1;
    }

    /// <summary>Gets the bounding rectangle of the full virtual desktop (all monitors).</summary>
    internal static (int Left, int Top, int Right, int Bottom) GetVirtualScreenBounds()
    {
        var left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return (left, top, left + width - 1, top + height - 1);
    }

    #endregion

    #region SetThreadExecutionState (sleep prevention)

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint SetThreadExecutionState(uint esFlags);

    internal const uint ES_CONTINUOUS = 0x80000000;
    internal const uint ES_SYSTEM_REQUIRED = 0x00000001;
    internal const uint ES_DISPLAY_REQUIRED = 0x00000002;

    #endregion
}
