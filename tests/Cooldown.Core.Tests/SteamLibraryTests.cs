using Cooldown.Steam;

namespace Cooldown.Core.Tests;

public sealed class SteamLibraryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cooldown-steam-").FullName;

    [Fact]
    public void Finds_games_across_library_folders()
    {
        var second = Path.Combine(_root, "extra-library");
        WriteManifest(_root, 730, "Counter-Strike 2", "Counter-Strike Global Offensive");
        WriteManifest(second, 1145360, "Hades", "Hades");
        File.WriteAllText(Path.Combine(_root, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders" { "0" { "path" "{{Escape(_root)}}" } "1" { "path" "{{Escape(second)}}" } }
            """);

        var library = new SteamLibrary(_root);

        Assert.Equal(2, library.GetInstalledGames().Count);
        var hades = library.Find(1145360)!;
        Assert.Equal("Hades", hades.Name);
        Assert.Equal(Path.Combine(second, "steamapps", "common", "Hades"), hades.InstallPath);
    }

    private static void WriteManifest(string library, int appId, string name, string installDir)
    {
        var steamapps = Directory.CreateDirectory(Path.Combine(library, "steamapps")).FullName;
        File.WriteAllText(Path.Combine(steamapps, $"appmanifest_{appId}.acf"), $$"""
            "AppState" { "appid" "{{appId}}" "name" "{{name}}" "installdir" "{{installDir}}" }
            """);
    }

    private static string Escape(string path) => path.Replace(@"\", @"\\");

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
