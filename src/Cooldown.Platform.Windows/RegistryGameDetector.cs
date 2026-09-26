using System.Runtime.Versioning;
using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;
using Cooldown.Steam;

namespace Cooldown.Platform.Windows;

/// <summary>Detects the running game from the RunningAppID value Steam keeps in the registry.</summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryGameDetector(SteamLibrary library) : IGameDetector
{
    public DetectedGame? GetRunningGame()
    {
        int appId = SteamRegistry.ReadRunningAppId();
        return appId > 0 ? new DetectedGame(appId, library.Find(appId)?.Name) : null;
    }
}
