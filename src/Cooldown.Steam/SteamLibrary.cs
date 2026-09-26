namespace Cooldown.Steam;

public sealed record InstalledGame(int AppId, string Name, string InstallPath);

/// <summary>Reads Steam's library folders and app manifests to map App IDs to names and install paths.</summary>
public sealed class SteamLibrary(string steamRoot)
{
    private Dictionary<int, InstalledGame>? _cache;

    public string SteamRoot { get; } = steamRoot;

    public IReadOnlyList<string> GetLibraryPaths()
    {
        var paths = new List<string> { SteamRoot };
        var file = Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(file))
            return paths;

        var folders = Vdf.Parse(File.ReadAllText(file)).Child("libraryfolders");
        if (folders is null)
            return paths;

        foreach (var entry in folders.Values.OfType<Dictionary<string, object>>())
        {
            if (entry.Value("path") is { Length: > 0 } path)
                paths.Add(path);
        }

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return paths.Select(p => Path.GetFullPath(p)).Distinct(comparer).ToList();
    }

    public IReadOnlyCollection<InstalledGame> GetInstalledGames() => Load().Values;

    public InstalledGame? Find(int appId)
    {
        if (Load().TryGetValue(appId, out var game))
            return game;

        // A game installed after we started won't be cached yet.
        Refresh();
        return Load().GetValueOrDefault(appId);
    }

    public void Refresh() => _cache = null;

    private Dictionary<int, InstalledGame> Load()
    {
        if (_cache is not null)
            return _cache;

        var games = new Dictionary<int, InstalledGame>();
        foreach (var library in GetLibraryPaths())
        {
            var steamapps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamapps))
                continue;

            foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                try
                {
                    var app = Vdf.Parse(File.ReadAllText(manifest)).Child("AppState");
                    if (app is null || !int.TryParse(app.Value("appid"), out var appId))
                        continue;

                    var name = app.Value("name") ?? $"App {appId}";
                    var installDir = app.Value("installdir") ?? "";
                    games[appId] = new InstalledGame(appId, name, Path.Combine(steamapps, "common", installDir));
                }
                catch (Exception ex) when (ex is FormatException or IOException)
                {
                    // Steam rewrites these files while updating. Skip and pick it up next refresh.
                }
            }
        }
        return _cache = games;
    }
}
