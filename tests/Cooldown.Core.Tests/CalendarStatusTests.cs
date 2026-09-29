using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.Core.Tests;

public class CalendarStatusTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 26, 12, 0, 0, TimeSpan.FromHours(-7));
    private const int DayStartHour = 4;

    private static (BudgetService, InMemorySessionStore) Setup(List<Bucket> buckets, Dictionary<int, List<string>> assignments)
    {
        var config = new CooldownConfig { Buckets = buckets, Assignments = assignments, DayStartHour = DayStartHour };
        var store = new InMemorySessionStore();
        return (new BudgetService(config, store), store);
    }

    [Fact]
    public void A_day_after_today_has_not_happened_yet()
    {
        var (budgets, _) = Setup([], []);
        var result = CalendarStatus.ForDay(budgets, DateOnly.FromDateTime(Noon.AddDays(1).Date), Noon, DayStartHour, showGoals: true, showLimits: true);
        Assert.Equal(DayResult.NotYetHappened, result);
    }

    [Fact]
    public void No_criteria_selected_reports_NoCriteria()
    {
        var (budgets, _) = Setup([], []);
        var result = CalendarStatus.ForDay(budgets, DateOnly.FromDateTime(Noon.Date), Noon, DayStartHour, showGoals: false, showLimits: false);
        Assert.Equal(DayResult.NoCriteria, result);
    }

    [Fact]
    public void Limits_only_fails_the_day_a_countdown_bucket_went_over()
    {
        var bucket = new Bucket("comp", "Competitive", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Block);
        var (budgets, store) = Setup([bucket], new() { [730] = ["comp"] });
        store.Extend(store.Begin(730, Noon.AddHours(-6)).Id, Noon); // 6h today, way over the 1h budget

        var today = DateOnly.FromDateTime(Noon.Date);
        var result = CalendarStatus.ForDay(budgets, today, Noon, DayStartHour, showGoals: false, showLimits: true);
        Assert.Equal(DayResult.Fail, result);
    }

    [Fact]
    public void Limits_only_passes_the_day_every_countdown_bucket_stayed_under()
    {
        var bucket = new Bucket("comp", "Competitive", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Block);
        var (budgets, store) = Setup([bucket], new() { [730] = ["comp"] });
        store.Extend(store.Begin(730, Noon.AddMinutes(-20)).Id, Noon); // 20m, under the 1h budget

        var today = DateOnly.FromDateTime(Noon.Date);
        var result = CalendarStatus.ForDay(budgets, today, Noon, DayStartHour, showGoals: false, showLimits: true);
        Assert.Equal(DayResult.Pass, result);
    }

    [Fact]
    public void Goals_only_fails_until_the_goal_bucket_is_met()
    {
        var goal = new Bucket("learning", "Learning", TimeSpan.FromHours(2), ResetPeriod.Monthly, Enforcement.Remind, IsGoal: true);
        var (budgets, store) = Setup([goal], new() { [400] = ["learning"] });
        store.Extend(store.Begin(400, Noon.AddHours(-1)).Id, Noon); // only 1h of a 2h goal

        var today = DateOnly.FromDateTime(Noon.Date);
        var result = CalendarStatus.ForDay(budgets, today, Noon, DayStartHour, showGoals: true, showLimits: false);
        Assert.Equal(DayResult.Fail, result);
    }

    [Fact]
    public void Goals_only_passes_once_the_goal_bucket_is_met()
    {
        var goal = new Bucket("learning", "Learning", TimeSpan.FromHours(2), ResetPeriod.Monthly, Enforcement.Remind, IsGoal: true);
        var (budgets, store) = Setup([goal], new() { [400] = ["learning"] });
        store.Extend(store.Begin(400, Noon.AddHours(-3)).Id, Noon); // 3h, past the 2h goal

        var today = DateOnly.FromDateTime(Noon.Date);
        var result = CalendarStatus.ForDay(budgets, today, Noon, DayStartHour, showGoals: true, showLimits: false);
        Assert.Equal(DayResult.Pass, result);
    }

    [Fact]
    public void Show_all_fails_if_either_limits_or_goals_missed_even_if_the_other_is_fine()
    {
        var limit = new Bucket("comp", "Competitive", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Block);
        var goal = new Bucket("learning", "Learning", TimeSpan.FromHours(2), ResetPeriod.Monthly, Enforcement.Remind, IsGoal: true);
        var (budgets, store) = Setup([limit, goal], new() { [730] = ["comp"], [400] = ["learning"] });

        store.Extend(store.Begin(730, Noon.AddMinutes(-20)).Id, Noon); // under the limit: fine
        store.Extend(store.Begin(400, Noon.AddHours(-1)).Id, Noon); // only 1h of a 2h goal: not met

        var today = DateOnly.FromDateTime(Noon.Date);
        var result = CalendarStatus.ForDay(budgets, today, Noon, DayStartHour, showGoals: true, showLimits: true);
        Assert.Equal(DayResult.Fail, result);
    }
}
