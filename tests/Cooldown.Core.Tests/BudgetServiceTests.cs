using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.Core.Tests;

public class BudgetServiceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 26, 12, 0, 0, TimeSpan.FromHours(-7));

    private static (BudgetService, InMemorySessionStore, Bucket) Setup()
    {
        var bucket = new Bucket("comp", "Competitive", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Block);
        var config = new CooldownConfig { Buckets = [bucket], Assignments = { [730] = ["comp"], [570] = ["comp"] } };
        var store = new InMemorySessionStore();
        return (new BudgetService(config, store), store, bucket);
    }

    [Fact]
    public void Sums_all_games_in_the_bucket()
    {
        var (budgets, store, bucket) = Setup();
        store.Extend(store.Begin(730, Noon.AddHours(-2)).Id, Noon.AddHours(-2).AddMinutes(20));
        store.Extend(store.Begin(570, Noon.AddHours(-1)).Id, Noon.AddHours(-1).AddMinutes(15));

        Assert.Equal(TimeSpan.FromMinutes(35), budgets.Used(bucket, Noon));
        Assert.Equal(TimeSpan.FromMinutes(25), budgets.Status(bucket, Noon).Remaining);
    }

    [Fact]
    public void Ignores_games_outside_the_bucket()
    {
        var (budgets, store, bucket) = Setup();
        store.Extend(store.Begin(999, Noon.AddHours(-1)).Id, Noon);
        Assert.Equal(TimeSpan.Zero, budgets.Used(bucket, Noon));
    }

    [Fact]
    public void Clips_sessions_that_started_before_the_period()
    {
        var (budgets, store, bucket) = Setup();
        // 3:30 to 4:20. Only the 20 minutes after the 4:00 reset count.
        var start = new DateTimeOffset(2026, 9, 26, 3, 30, 0, Noon.Offset);
        store.Extend(store.Begin(730, start).Id, start.AddMinutes(50));

        Assert.Equal(TimeSpan.FromMinutes(20), budgets.Used(bucket, Noon));
    }

    [Fact]
    public void Remaining_never_goes_negative()
    {
        var (budgets, store, bucket) = Setup();
        store.Extend(store.Begin(730, Noon.AddHours(-3)).Id, Noon);
        var status = budgets.Status(bucket, Noon);
        Assert.Equal(TimeSpan.Zero, status.Remaining);
        Assert.True(status.IsExhausted);
    }

    [Fact]
    public void A_game_in_two_buckets_counts_independently_toward_each()
    {
        var learning = new Bucket("learning", "Learning", TimeSpan.FromHours(2), ResetPeriod.Monthly, Enforcement.Remind, IsGoal: true);
        var strategy = new Bucket("strategy", "Strategy", TimeSpan.FromHours(5), ResetPeriod.Weekly, Enforcement.Block);
        var config = new CooldownConfig
        {
            Buckets = [learning, strategy],
            Assignments = { [400] = ["learning", "strategy"] },
        };
        var store = new InMemorySessionStore();
        var budgets = new BudgetService(config, store);
        store.Extend(store.Begin(400, Noon.AddHours(-1)).Id, Noon);

        Assert.Equal(TimeSpan.FromHours(1), budgets.Used(learning, Noon));
        Assert.Equal(TimeSpan.FromHours(1), budgets.Used(strategy, Noon));
        Assert.Equal([learning, strategy], budgets.BucketsFor(400));
    }
}
