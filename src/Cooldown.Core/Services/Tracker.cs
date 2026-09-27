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

    // Per-bucket state, since a game can now count toward more than one bucket at once and each
    // bucket has its own reset period and warning thresholds.
    private readonly Dictionary<string, string> _periodKeyByBucket = [];
    private readonly Dictionary<string, HashSet<int>> _warnedByBucket = [];
    private DateTimeOffset? _closeAt;
    private string? _closingBucketId;

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
        {
            _closeAt = null;
            _closingBucketId = null;
        }
        else
        {
            await EnforceAsync(game, now, ct);
        }

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
        // Goal buckets track progress passively (via BudgetService.Used, from recorded sessions)
        // but are never enforced: no warnings, no closing.
        var buckets = budgets.BucketsFor(game.AppId).Where(b => !b.IsGoal).ToList();

        Bucket? exhaustedBlock = null;
        foreach (var bucket in buckets)
        {
            var warned = WarnedSetFor(bucket, now);
            var status = budgets.Status(bucket, now);

            if (!status.IsExhausted)
            {
                WarnIfLow(bucket, status.Remaining, warned);
                continue;
            }

            if (bucket.Enforcement == Enforcement.Remind)
            {
                if (warned.Add(0))
                    notifier.Notify("Budget used up",
                        $"{bucket.Name} is out of time {Periods.Noun(bucket.Period)}.", NotificationLevel.Alert);
                continue;
            }

            // If several Block buckets are exhausted at once, the first one found drives the close countdown.
            exhaustedBlock ??= bucket;
        }

        if (exhaustedBlock is null)
        {
            _closeAt = null;
            _closingBucketId = null;
            return;
        }

        if (_closeAt is null || _closingBucketId != exhaustedBlock.Id)
        {
            bool justLaunched = _current?.Start == now;
            int grace = justLaunched ? config.LaunchGraceSeconds : config.GraceSeconds;
            _closeAt = now + TimeSpan.FromSeconds(grace * clock.Speed);
            _closingBucketId = exhaustedBlock.Id;
            notifier.Notify("Time's up",
                $"{exhaustedBlock.Name} is out of time {Periods.Noun(exhaustedBlock.Period)}. " +
                $"{game.DisplayName} will close in {grace} seconds. Save now.",
                NotificationLevel.Alert);
            return;
        }

        if (now >= _closeAt)
        {
            Log.Info($"Closing {game.DisplayName} ({game.AppId})");
            _closeAt = null;
            _closingBucketId = null;
            bool closed = await terminator.TerminateAsync(game.AppId, TimeSpan.FromSeconds(10), ct);
            if (!closed)
                notifier.Notify("Couldn't close game",
                    $"{game.DisplayName} is still running. {exhaustedBlock.Name} is out of time.",
                    NotificationLevel.Alert);
        }
    }

    /// <summary>The set of warning thresholds already fired for this bucket's current period, reset on rollover.</summary>
    private HashSet<int> WarnedSetFor(Bucket bucket, DateTimeOffset now)
    {
        var key = $"{bucket.Id}@{budgets.PeriodStart(bucket, now):O}";
        if (_periodKeyByBucket.GetValueOrDefault(bucket.Id) != key)
        {
            _periodKeyByBucket[bucket.Id] = key;
            _warnedByBucket[bucket.Id] = [];
        }
        return _warnedByBucket[bucket.Id];
    }

    private void WarnIfLow(Bucket bucket, TimeSpan remaining, HashSet<int> warned)
    {
        // Mark every threshold we've passed so a late start doesn't fire three warnings in a row.
        var crossed = config.WarningMinutes
            .Where(m => m > 0 && remaining <= TimeSpan.FromMinutes(m) && !warned.Contains(m))
            .ToList();
        if (crossed.Count == 0)
            return;

        warned.UnionWith(crossed);
        notifier.Notify("Time check",
            $"{Format.Duration(remaining)} left in {bucket.Name} {Periods.Noun(bucket.Period)}.",
            NotificationLevel.Warning);
    }
}
