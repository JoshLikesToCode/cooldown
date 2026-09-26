using Cooldown.Core;
using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;
using Cooldown.Core.Services;
using Cooldown.Data;
using Cooldown.Platform.Fake;
using Cooldown.Platform.Windows;
using Cooldown.Steam;

namespace Cooldown.App;

/// <summary>Composition root. Picks the real Windows pieces or the fake ones and wires everything up.</summary>
public sealed class AppHost : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SqliteSessionStore _store;
    private Task? _loop;

    public CooldownConfig Config { get; }
    public BudgetService Budgets { get; }
    public Tracker Tracker { get; }
    public IClock Clock { get; }
    public string DataDir { get; }
    public string ConfigPath => Path.Combine(DataDir, "config.json");

    /// <summary>Shown in the window footer. Mode info or setup problems.</summary>
    public string Status { get; }

    public AppHost(AppOptions options, INotifier notifier)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        DataDir = Path.Combine(appData, options.Fake ? "Cooldown-fake" : "Cooldown");
        Log.Init(Path.Combine(DataDir, "logs", "cooldown.log"));
        Log.Info($"Starting. Fake={options.Fake} Data={DataDir}");

        IGameDetector detector;
        IGameTerminator terminator;

        if (options.Fake)
        {
            // Fresh start every run, since the fast clock makes stored times meaningless.
            var dbPath = Path.Combine(DataDir, "sessions.db");
            foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                File.Delete(f);

            Clock = new ScaledClock(options.Speed);
            Config = ConfigStore.LoadOrCreate(ConfigPath, FakeDefaults);
            var fake = options.Scenario is { } path ? FakeSteam.FromFile(path, Clock) : FakeSteam.Default(Clock);
            detector = fake;
            terminator = fake;
            Status = $"Fake mode at {options.Speed}x speed";
        }
        else if (OperatingSystem.IsWindows() && SteamRegistry.FindSteamRoot() is { } steamRoot)
        {
            Clock = new SystemClock();
            Config = ConfigStore.LoadOrCreate(ConfigPath, CooldownConfig.CreateDefault);
            var library = new SteamLibrary(steamRoot);
            WriteGameList(library);
            detector = new RegistryGameDetector(library);
            terminator = new WindowsGameTerminator(library);
            Status = $"Watching Steam at {steamRoot}";
        }
        else
        {
            Clock = new SystemClock();
            Config = ConfigStore.LoadOrCreate(ConfigPath, CooldownConfig.CreateDefault);
            detector = new NothingRunning();
            terminator = new NothingRunning();
            Status = "Steam not found. Is it installed for this user?";
            Log.Error(Status);
        }

        _store = new SqliteSessionStore(Path.Combine(DataDir, "sessions.db"));
        Budgets = new BudgetService(Config, _store);
        Tracker = new Tracker(Config, detector, terminator, notifier, _store, Budgets, Clock);
    }

    public void Start() => _loop = Task.Run(() => Tracker.RunAsync(_cts.Token));

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { /* cancellation */ }
        _store.Dispose();
        _cts.Dispose();
    }

    /// <summary>Lists installed games with their App IDs so users can fill in config.json.</summary>
    private void WriteGameList(SteamLibrary library)
    {
        var lines = library.GetInstalledGames()
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.AppId,-10} {g.Name}");
        File.WriteAllLines(Path.Combine(DataDir, "installed-games.txt"), lines);
    }

    private static CooldownConfig FakeDefaults()
    {
        var config = CooldownConfig.CreateDefault();
        config.Assignments[730] = "competitive";   // Counter-Strike 2
        config.Assignments[1145360] = "story";     // Hades
        config.PollSeconds = 1;
        config.GraceSeconds = 10;
        config.LaunchGraceSeconds = 5;
        return config;
    }

    private sealed class NothingRunning : IGameDetector, IGameTerminator
    {
        public DetectedGame? GetRunningGame() => null;
        public Task<bool> TerminateAsync(int appId, TimeSpan t, CancellationToken ct) => Task.FromResult(false);
    }
}
