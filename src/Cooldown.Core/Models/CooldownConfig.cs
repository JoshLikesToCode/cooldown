using System.Text.Json.Serialization;

namespace Cooldown.Core.Models;

/// <summary>User settings. Stored as config.json in the app data folder.</summary>
public sealed class CooldownConfig
{
    public List<Bucket> Buckets { get; set; } = [];

    /// <summary>Steam App ID to the bucket IDs it counts toward. Games not listed here are never limited.</summary>
    [JsonConverter(typeof(AssignmentsJsonConverter))]
    public Dictionary<int, List<string>> Assignments { get; set; } = [];

    /// <summary>Hour a "day" starts. 4 means late-night sessions count toward the previous day.</summary>
    public int DayStartHour { get; set; } = 4;

    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;

    /// <summary>Minutes remaining at which to warn.</summary>
    public int[] WarningMinutes { get; set; } = [10, 5, 1];

    /// <summary>Time to save before a running game is closed.</summary>
    public int GraceSeconds { get; set; } = 60;

    /// <summary>Shorter grace when a game is launched after its bucket is already empty.</summary>
    public int LaunchGraceSeconds { get; set; } = 15;

    public int PollSeconds { get; set; } = 5;

    /// <summary>Which per-game playtime totals the Assignments tab shows. All default on.</summary>
    public bool ShowDailyPlaytime { get; set; } = true;

    public bool ShowWeeklyPlaytime { get; set; } = true;

    public bool ShowAllTimePlaytime { get; set; } = true;

    public static CooldownConfig CreateDefault() => new()
    {
        Buckets =
        [
            new("competitive", "Competitive", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Block),
            new("story", "Story games", TimeSpan.FromHours(10), ResetPeriod.Weekly, Enforcement.Remind),
        ],
    };
}
