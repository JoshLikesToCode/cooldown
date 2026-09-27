using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;
using Cooldown.Steam;

namespace Cooldown.Platform.Windows;

/// <summary>Adapts <see cref="SteamLibrary"/> to the platform-independent catalog abstraction.</summary>
public sealed class SteamGameCatalog(SteamLibrary library) : IGameCatalog
{
    public IReadOnlyCollection<DetectedGame> GetInstalledGames() =>
        library.GetInstalledGames().Select(g => new DetectedGame(g.AppId, g.Name)).ToList();
}
