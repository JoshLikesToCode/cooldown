namespace Cooldown.Core.Services;

/// <summary>The pop-out calendar's verdict for one day.</summary>
public enum DayResult
{
    /// <summary>The day hasn't started yet (its Cooldown-day start is still in the future).</summary>
    NotYetHappened,

    /// <summary>Neither Show Goals nor Show Limits is on, so there's nothing to judge.</summary>
    NoCriteria,

    Pass,
    Fail,
}

/// <summary>
/// Decides the calendar's per-day verdict. Reuses BudgetService.Used (seeded with "as of the end
/// of that day") rather than re-deriving period math, so a weekly or monthly bucket's status
/// naturally accumulates day over day exactly like its own countdown/progress bar does.
/// </summary>
public static class CalendarStatus
{
    public static DayResult ForDay(
        BudgetService budgets, DateOnly day, DateTimeOffset now, int dayStartHour, bool showGoals, bool showLimits)
    {
        var dayStart = new DateTimeOffset(day.Year, day.Month, day.Day, dayStartHour, 0, 0, now.Offset);
        if (dayStart > now)
            return DayResult.NotYetHappened;

        if (!showGoals && !showLimits)
            return DayResult.NoCriteria;

        var dayEnd = dayStart.AddDays(1);
        var asOf = dayEnd > now ? now : dayEnd;

        bool limitsOk = !showLimits || budgets.WithinAllLimits(asOf);
        bool goalsOk = !showGoals || budgets.MetAllGoals(asOf);
        return limitsOk && goalsOk ? DayResult.Pass : DayResult.Fail;
    }
}
