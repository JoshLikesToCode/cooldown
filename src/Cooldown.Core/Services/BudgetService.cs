using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;

namespace Cooldown.Core.Services;

public sealed class BudgetService(CooldownConfig config, ISessionStore store)
{
    /// <summary>All buckets a game counts toward. Usually zero or one, but a game can be in several.</summary>
    public IReadOnlyList<Bucket> BucketsFor(int appId) =>
        config.Assignments.TryGetValue(appId, out var bucketIds)
            ? config.Buckets.Where(b => bucketIds.Contains(b.Id)).ToList()
            : [];

    public DateTimeOffset PeriodStart(Bucket bucket, DateTimeOffset now) =>
        Periods.CurrentStart(bucket.Period, now, config.DayStartHour, config.WeekStart);

    public TimeSpan Used(Bucket bucket, DateTimeOffset now)
    {
        var start = PeriodStart(bucket, now);
        var appIds = config.Assignments
            .Where(kv => kv.Value.Contains(bucket.Id))
            .Select(kv => kv.Key)
            .ToHashSet();

        var total = TimeSpan.Zero;
        foreach (var s in store.GetSessionsSince(start))
        {
            if (!appIds.Contains(s.AppId))
                continue;

            // Clip sessions that straddle the period boundary.
            var from = s.Start < start ? start : s.Start;
            var to = s.End > now ? now : s.End;
            if (to > from)
                total += to - from;
        }
        return total;
    }

    public BucketStatus Status(Bucket bucket, DateTimeOffset now)
    {
        var used = Used(bucket, now);
        var remaining = bucket.Budget - used;
        return new BucketStatus(bucket, used, remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining);
    }

    public IReadOnlyList<BucketStatus> Snapshot(DateTimeOffset now) =>
        config.Buckets.Select(b => Status(b, now)).ToList();

    /// <summary>
    /// Total time since <paramref name="since"/> spent on games that count toward at least one
    /// countdown (non-goal) bucket. A game in both a countdown and a goal bucket counts in both totals.
    /// </summary>
    public TimeSpan CountdownPlaytime(DateTimeOffset since, DateTimeOffset now) =>
        PlaytimeWhere(since, now, buckets => buckets.Any(b => !b.IsGoal));

    /// <summary>Total time since <paramref name="since"/> spent on games that count toward at least one goal bucket.</summary>
    public TimeSpan GoalPlaytime(DateTimeOffset since, DateTimeOffset now) =>
        PlaytimeWhere(since, now, buckets => buckets.Any(b => b.IsGoal));

    private TimeSpan PlaytimeWhere(DateTimeOffset since, DateTimeOffset now, Func<IReadOnlyList<Bucket>, bool> matches)
    {
        var total = TimeSpan.Zero;
        foreach (var s in store.GetSessionsSince(since))
        {
            if (!matches(BucketsFor(s.AppId)))
                continue;

            var from = s.Start < since ? since : s.Start;
            var to = s.End > now ? now : s.End;
            if (to > from)
                total += to - from;
        }
        return total;
    }
}
