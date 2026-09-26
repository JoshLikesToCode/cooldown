using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;

namespace Cooldown.Core.Services;

/// <summary>
/// The main loop. Each tick it records playtime for the running game,
/// sends warnings as a bucket runs low, and closes the game when a Block bucket is empty.
/// </summary>
public sealed class Tracker(
    CooldownConfig config,
    IGameDetector detector,
    IGameTerminator terminator,
    INotifier notifier,
    ISessionStore store,
    BudgetService budgets,
    IClock clock)
{
    private sealed record Current(long SessionId, int AppId, DateTimeOffset Start, DateTimeOffset LastSeen);

    private Current? _current;
    private string? _periodKey;
    private readonly HashSet<int> _warned = [];
    private DateTimeOffset? _closeAt;

    public DetectedGame? RunningGame { get; private set; }

    /// <summary>Raised after every tick. Handlers run on the tracker's thread.</summary>
    public event Action? Ticked;

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.PollSeconds));
        do
        {
            try
            {
                await TickAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error("Tick failed", ex);
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task TickAsync(CancellationToken ct = default)
    {
        var now = clock.Now;
        var game = detector.GetRunningGame();

        if (game?.AppId != RunningGame?.AppId)
            Log.Info(game is null ? "No game running" : $"Detected {game.DisplayName} ({game.AppId})");

        RunningGame = game;
        RecordPlaytime(game, now);

        if (game is null)
            _closeAt = null;
        else
            await EnforceAsync(game, now, ct);

        Ticked?.Invoke();
    }

    private void RecordPlaytime(DetectedGame? game, DateTimeOffset now)
    {
        // A gap longer than a few polls means the PC slept or we stalled. Don't count it.
        var maxGap = TimeSpan.FromSeconds(config.PollSeconds * 3 * clock.Speed);

        if (_current is { } cur)
        {
            bool sameGame = game?.AppId == cur.AppId;
            bool continuous = now - cur.LastSeen <= maxGap;
            if (sameGame && continuous)
            {
                store.Extend(cur.SessionId, now);
                _current = cur with { LastSeen = now };
                return;
            }
            _current = null;
        }

        if (game is not null)
        {
            var session = store.Begin(game.AppId, now);
            _current = new Current(session.Id, game.AppId, now, now);
        }
    }

    private async Task EnforceAsync(DetectedGame game, DateTimeOffset now, CancellationToken ct)
    {
        var bucket = budgets.BucketFor(game.AppId);
        if (bucket is null)
            return;

        // Reset warning state when the bucket or period changes.
        var key = $"{bucket.Id}@{budgets.PeriodStart(bucket, now):O}";
        if (key != _periodKey)
        {
            _periodKey = key;
            _warned.Clear();
            _closeAt = null;
        }

        var status = budgets.Status(bucket, now);

        if (!status.IsExhausted)
        {
            _closeAt = null;
            WarnIfLow(bucket, status.Remaining);
            return;
        }

        if (bucket.Enforcement == Enforcement.Remind)
        {
            if (_warned.Add(0))
                notifier.Notify("Budget used up",
                    $"{bucket.Name} is out of time {Periods.Noun(bucket.Period)}.", NotificationLevel.Alert);
            return;
        }

        if (_closeAt is null)
        {
            bool justLaunched = _current?.Start == now;
            int grace = justLaunched ? config.LaunchGraceSeconds : config.GraceSeconds;
            _closeAt = now + TimeSpan.FromSeconds(grace * clock.Speed);
            notifier.Notify("Time's up",
                $"{bucket.Name} is out of time {Periods.Noun(bucket.Period)}. " +
                $"{game.DisplayName} will close in {grace} seconds. Save now.",
                NotificationLevel.Alert);
            return;
        }

        if (now >= _closeAt)
        {
            Log.Info($"Closing {game.DisplayName} ({game.AppId})");
            _closeAt = null;
            bool closed = await terminator.TerminateAsync(game.AppId, TimeSpan.FromSeconds(10), ct);
            if (!closed)
                notifier.Notify("Couldn't close game",
                    $"{game.DisplayName} is still running. {bucket.Name} is out of time.",
                    NotificationLevel.Alert);
        }
    }

    private void WarnIfLow(Bucket bucket, TimeSpan remaining)
    {
        // Mark every threshold we've passed so a late start doesn't fire three warnings in a row.
        var crossed = config.WarningMinutes
            .Where(m => m > 0 && remaining <= TimeSpan.FromMinutes(m) && !_warned.Contains(m))
            .ToList();
        if (crossed.Count == 0)
            return;

        _warned.UnionWith(crossed);
        notifier.Notify("Time check",
            $"{Format.Duration(remaining)} left in {bucket.Name} {Periods.Noun(bucket.Period)}.",
            NotificationLevel.Warning);
    }
}
