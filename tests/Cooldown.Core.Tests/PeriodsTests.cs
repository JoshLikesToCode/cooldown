using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.Core.Tests;

public class PeriodsTests
{
    private static readonly TimeSpan Pacific = TimeSpan.FromHours(-7);

    [Fact]
    public void Daily_period_starts_at_day_start_hour()
    {
        var now = new DateTimeOffset(2026, 9, 26, 15, 0, 0, Pacific);
        var start = Periods.CurrentStart(ResetPeriod.Daily, now, 4, DayOfWeek.Monday);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 4, 0, 0, Pacific), start);
    }

    [Fact]
    public void Late_night_counts_toward_previous_day()
    {
        var now = new DateTimeOffset(2026, 9, 27, 2, 30, 0, Pacific);
        var start = Periods.CurrentStart(ResetPeriod.Daily, now, 4, DayOfWeek.Monday);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 4, 0, 0, Pacific), start);
    }

    [Fact]
    public void Weekly_period_starts_on_week_start_day()
    {
        // Saturday Sept 26, 2026. Week started Monday Sept 21.
        var now = new DateTimeOffset(2026, 9, 26, 15, 0, 0, Pacific);
        var start = Periods.CurrentStart(ResetPeriod.Weekly, now, 4, DayOfWeek.Monday);
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 4, 0, 0, Pacific), start);
    }

    [Fact]
    public void Monday_before_day_start_belongs_to_previous_week()
    {
        var now = new DateTimeOffset(2026, 9, 28, 1, 0, 0, Pacific);
        var start = Periods.CurrentStart(ResetPeriod.Weekly, now, 4, DayOfWeek.Monday);
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 4, 0, 0, Pacific), start);
    }

    [Fact]
    public void Monthly_period_starts_on_the_first_at_day_start_hour()
    {
        var now = new DateTimeOffset(2026, 9, 26, 15, 0, 0, Pacific);
        var start = Periods.CurrentStart(ResetPeriod.Monthly, now, 4, DayOfWeek.Monday);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 4, 0, 0, Pacific), start);
    }

    [Fact]
    public void Start_of_month_before_day_start_belongs_to_previous_month()
    {
        var now = new DateTimeOffset(2026, 9, 1, 1, 0, 0, Pacific);
        var start = Periods.CurrentStart(ResetPeriod.Monthly, now, 4, DayOfWeek.Monday);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 4, 0, 0, Pacific), start);
    }
}
