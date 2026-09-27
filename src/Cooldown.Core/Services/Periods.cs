using Cooldown.Core.Models;

namespace Cooldown.Core.Services;

public static class Periods
{
    /// <summary>Start of the budget period containing <paramref name="now"/>, in now's time zone offset.</summary>
    public static DateTimeOffset CurrentStart(
        ResetPeriod period, DateTimeOffset now, int dayStartHour, DayOfWeek weekStart)
    {
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, dayStartHour, 0, 0, now.Offset);
        if (now < dayStart)
            dayStart = dayStart.AddDays(-1);

        if (period == ResetPeriod.Daily)
            return dayStart;

        if (period == ResetPeriod.Weekly)
        {
            int daysSinceWeekStart = ((int)dayStart.DayOfWeek - (int)weekStart + 7) % 7;
            return dayStart.AddDays(-daysSinceWeekStart);
        }

        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, dayStartHour, 0, 0, now.Offset);
        return now < monthStart ? monthStart.AddMonths(-1) : monthStart;
    }

    public static string Noun(ResetPeriod period) => period switch
    {
        ResetPeriod.Daily => "today",
        ResetPeriod.Weekly => "this week",
        _ => "this month",
    };
}
