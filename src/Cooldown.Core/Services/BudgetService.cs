using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;

namespace Cooldown.Core.Services;

public sealed class BudgetService(CooldownConfig config, ISessionStore store)
{
    public Bucket? BucketFor(int appId) =>
        config.Assignments.TryGetValue(appId, out var bucketId)
            ? config.Buckets.FirstOrDefault(b => b.Id == bucketId)
            : null;

    public DateTimeOffset PeriodStart(Bucket bucket, DateTimeOffset now) =>
        Periods.CurrentStart(bucket.Period, now, config.DayStartHour, config.WeekStart);

    public TimeSpan Used(Bucket bucket, DateTimeOffset now)
    {
        var start = PeriodStart(bucket, now);
        var appIds = config.Assignments
            .Where(kv => kv.Value == bucket.Id)
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
}
