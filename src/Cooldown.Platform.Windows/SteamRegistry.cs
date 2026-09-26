using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Cooldown.Platform.Windows;

[SupportedOSPlatform("windows")]
public static class SteamRegistry
{
    private const string SteamKey = @"Software\Valve\Steam";

    /// <summary>Steam's install folder, e.g. C:\Program Files (x86)\Steam.</summary>
    public static string? FindSteamRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
        return (key?.GetValue("SteamPath") as string)?.Replace('/', '\\');
    }

    /// <summary>App ID of the game Steam says is running, or 0.</summary>
    public static int ReadRunningAppId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
        return key?.GetValue("RunningAppID") is int id ? id : 0;
    }
}
