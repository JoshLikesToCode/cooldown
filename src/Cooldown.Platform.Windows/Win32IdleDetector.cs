using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Cooldown.Core.Abstractions;

namespace Cooldown.Platform.Windows;

/// <summary>
/// Reads Windows' own system-wide idle counter (GetLastInputInfo) - the exact same API
/// screensavers and "away" statuses use. It only ever reads a tick count Windows already
/// maintains; it never hooks input, reads other processes' memory, or touches a game at all,
/// so it's a no-op as far as any anti-cheat system is concerned.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32IdleDetector : IIdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero;

        uint idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMs);
    }
}
