using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Cooldown.Core;
using Cooldown.Core.Abstractions;
using Cooldown.Steam;

namespace Cooldown.Platform.Windows;

/// <summary>
/// Finds a game's processes by install folder, asks them to close, then force-kills stragglers.
/// Never injects into or hooks the game, which keeps anti-cheat happy.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGameTerminator(SteamLibrary library) : IGameTerminator
{
    public async Task<bool> TerminateAsync(int appId, TimeSpan gracefulTimeout, CancellationToken ct)
    {
        var game = library.Find(appId);
        if (game is null)
        {
            Log.Error($"No install folder known for app {appId}");
            return false;
        }

        var processes = ProcessesUnder(game.InstallPath);
        if (processes.Count == 0)
        {
            Log.Error($"No processes found under {game.InstallPath}");
            return false;
        }

        try
        {
            foreach (var p in processes)
                TryRun(() => p.CloseMainWindow());

            var deadline = DateTime.UtcNow + gracefulTimeout;
            while (DateTime.UtcNow < deadline && processes.Any(IsAlive))
                await Task.Delay(500, ct);

            foreach (var p in processes.Where(IsAlive))
            {
                Log.Info($"Force-killing {p.ProcessName} ({p.Id})");
                TryRun(() => p.Kill(entireProcessTree: true));
            }

            await Task.Delay(1000, ct);
            return !processes.Any(IsAlive);
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    private static List<Process> ProcessesUnder(string installPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installPath)) + Path.DirectorySeparatorChar;
        var matches = new List<Process>();

        foreach (var p in Process.GetProcesses())
        {
            var path = ImagePath(p.Id);
            if (path is not null && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                matches.Add(p);
            else
                p.Dispose();
        }
        return matches;
    }

    private static bool IsAlive(Process p)
    {
        try { return !p.HasExited; }
        catch (InvalidOperationException) { return false; }
    }

    private static void TryRun(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    // QueryFullProcessImageName works with limited rights, unlike Process.MainModule,
    // which throws for many processes and is slow.
    private static string? ImagePath(int pid)
    {
        const uint ProcessQueryLimitedInformation = 0x1000;
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == 0)
            return null;
        try
        {
            var buffer = new StringBuilder(1024);
            uint size = (uint)buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);
}
