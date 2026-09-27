using System.Collections.Concurrent;
using Avalonia.Media.Imaging;

namespace Cooldown.App.Views;

/// <summary>
/// Fetches a small game thumbnail from Steam's public CDN (no API key needed) and caches it on disk,
/// so the Assignments list doesn't re-download the same image every time it refreshes. Fails silently -
/// offline, fake-mode App IDs, and 404s all just mean no icon for that row.
/// </summary>
internal static class SteamIconCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private static readonly ConcurrentDictionary<int, Task<Bitmap?>> InFlight = new();

    public static Task<Bitmap?> GetAsync(int appId, string cacheDir) =>
        InFlight.GetOrAdd(appId, id => LoadAsync(id, cacheDir));

    private static async Task<Bitmap?> LoadAsync(int appId, string cacheDir)
    {
        try
        {
            Directory.CreateDirectory(cacheDir);
            var path = Path.Combine(cacheDir, $"{appId}.jpg");
            if (!File.Exists(path))
            {
                var bytes = await Http.GetByteArrayAsync($"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/capsule_184x69.jpg");
                await File.WriteAllBytesAsync(path, bytes);
            }
            await using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
