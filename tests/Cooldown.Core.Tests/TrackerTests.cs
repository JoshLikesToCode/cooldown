using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.Core.Tests;

public class TrackerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 26, 12, 0, 0, TimeSpan.FromHours(-7));
    private static readonly DetectedGame Cs2 = new(730, "Counter-Strike 2");

    private sealed class Rig
    {
        public readonly ManualClock Clock = new(Noon);
        public readonly StubDetector Detector = new();
        public readonly RecordingNotifier Notifier = new();
        public readonly InMemorySessionStore Store = new();
        public readonly RecordingTerminator Terminator;
        public readonly Tracker Tracker;
        public readonly BudgetService Budgets;

        public Rig(Enforcement enforcement, TimeSpan budget)
            : this([new("comp", "Competitive", budget, ResetPeriod.Daily, enforcement)], new() { [730] = ["comp"] })
        {
        }

        public Rig(List<Bucket> buckets, Dictionary<int, List<string>> assignments)
        {
            var config = new CooldownConfig
            {
                Buckets = buckets,
                Assignments = assignments,
                PollSeconds = 5,
                GraceSeconds = 60,
                LaunchGraceSeconds = 15,
            };
            Terminator = new RecordingTerminator(Detector);
            Budgets = new BudgetService(config, Store);
            Tracker = new Tracker(config, Detector, Terminator, Notifier, Store, Budgets, Clock);
        }

        /// <summary>Tick every 5 seconds for the given duration.</summary>
        public async Task Play(TimeSpan duration)
        {
            for (var t = TimeSpan.Zero; t < duration; t += TimeSpan.FromSeconds(5))
            {
                await Tracker.TickAsync();
                Clock.Advance(TimeSpan.FromSeconds(5));
            }
            await Tracker.TickAsync();
        }
    }

    [Fact]
    public async Task Records_continuous_play_as_one_session()
    {
        var rig = new Rig(Enforcement.Remind, TimeSpan.FromHours(5));
        rig.Detector.Running = Cs2;
        await rig.Play(TimeSpan.FromMinutes(10));

        var session = Assert.Single(rig.Store.All);
        Assert.Equal(TimeSpan.FromMinutes(10), session.End - session.Start);
    }

    [Fact]
    public async Task Does_not_count_time_while_asleep()
    {
        var rig = new Rig(Enforcement.Remind, TimeSpan.FromHours(5));
        rig.Detector.Running = Cs2;
        await rig.Play(TimeSpan.FromMinutes(10));
        rig.Clock.Advance(TimeSpan.FromHours(2)); // laptop lid closed, game still "running"
        await rig.Play(TimeSpan.FromMinutes(5));

        Assert.Equal(2, rig.Store.All.Count);
        Assert.Equal(TimeSpan.FromMinutes(15), rig.Budgets.Used(rig.Budgets.BucketsFor(730).Single(), rig.Clock.Now));
    }

    [Fact]
    public async Task Warns_once_per_threshold()
    {
        var rig = new Rig(Enforcement.Remind, TimeSpan.FromMinutes(30));
        rig.Detector.Running = Cs2;
        await rig.Play(TimeSpan.FromMinutes(29.5));

        var warnings = rig.Notifier.Sent.Where(n => n.Level == NotificationLevel.Warning).ToList();
        Assert.Equal(3, warnings.Count); // 10, 5 and 1 minute
    }

    [Fact]
    public async Task Late_start_sends_one_warning_not_three()
    {
        var rig = new Rig(Enforcement.Remind, TimeSpan.FromMinutes(30));
        rig.Store.Extend(rig.Store.Begin(730, Noon.AddHours(-1)).Id, Noon.AddHours(-1).AddMinutes(29).AddSeconds(30));

        rig.Detector.Running = Cs2;
        await rig.Tracker.TickAsync();

        Assert.Single(rig.Notifier.Sent);
    }

    [Fact]
    public async Task Remind_bucket_never_closes_the_game()
    {
        var rig = new Rig(Enforcement.Remind, TimeSpan.FromMinutes(5));
        rig.Detector.Running = Cs2;
        await rig.Play(TimeSpan.FromMinutes(10));

        Assert.Empty(rig.Terminator.Terminated);
        Assert.Single(rig.Notifier.Sent, n => n.Title == "Budget used up");
    }

    [Fact]
    public async Task Block_bucket_closes_the_game_after_grace()
    {
        var rig = new Rig(Enforcement.Block, TimeSpan.FromMinutes(5));
        rig.Detector.Running = Cs2;

        await rig.Play(TimeSpan.FromMinutes(5));
        Assert.Empty(rig.Terminator.Terminated);
        Assert.Contains(rig.Notifier.Sent, n => n.Title == "Time's up");

        await rig.Play(TimeSpan.FromSeconds(55));
        Assert.Empty(rig.Terminator.Terminated);

        await rig.Play(TimeSpan.FromSeconds(10));
        Assert.Equal(new[] { 730 }, rig.Terminator.Terminated);
    }

    [Fact]
    public async Task Relaunching_an_empty_bucket_uses_the_short_grace()
    {
        var rig = new Rig(Enforcement.Block, TimeSpan.FromMinutes(5));
        rig.Store.Extend(rig.Store.Begin(730, Noon.AddHours(-1)).Id, Noon.AddHours(-1).AddMinutes(5));

        rig.Detector.Running = Cs2;
        await rig.Play(TimeSpan.FromSeconds(20));

        Assert.Contains(rig.Notifier.Sent, n => n.Message.Contains("15 seconds"));
        Assert.Equal(new[] { 730 }, rig.Terminator.Terminated);
    }

    [Fact]
    public async Task Unassigned_games_are_tracked_but_never_limited()
    {
        var rig = new Rig(Enforcement.Block, TimeSpan.FromMinutes(1));
        rig.Detector.Running = new DetectedGame(999, "Stardew Valley");
        await rig.Play(TimeSpan.FromMinutes(10));

        Assert.Single(rig.Store.All);
        Assert.Empty(rig.Notifier.Sent);
        Assert.Empty(rig.Terminator.Terminated);
    }

    [Fact]
    public async Task A_game_in_multiple_buckets_closes_when_either_block_bucket_empties()
    {
        var roomy = new Bucket("roomy", "Roomy", TimeSpan.FromHours(5), ResetPeriod.Weekly, Enforcement.Block);
        var tight = new Bucket("tight", "Tight", TimeSpan.FromMinutes(5), ResetPeriod.Daily, Enforcement.Block);
        var rig = new Rig([roomy, tight], new() { [730] = ["roomy", "tight"] });
        rig.Detector.Running = Cs2;

        await rig.Play(TimeSpan.FromMinutes(5));
        await rig.Play(TimeSpan.FromSeconds(65));

        // The tight bucket (5 minutes) closed the game even though roomy (5 hours) still has plenty left.
        Assert.Equal(new[] { 730 }, rig.Terminator.Terminated);
    }

    [Fact]
    public async Task Goal_buckets_are_never_enforced_but_still_track_progress()
    {
        var goal = new Bucket("learning", "Learning", TimeSpan.FromHours(2), ResetPeriod.Monthly, Enforcement.Block, IsGoal: true);
        var rig = new Rig([goal], new() { [730] = ["learning"] });
        rig.Detector.Running = Cs2;

        await rig.Play(TimeSpan.FromMinutes(10));

        Assert.Empty(rig.Notifier.Sent);
        Assert.Empty(rig.Terminator.Terminated);
        Assert.Equal(TimeSpan.FromMinutes(10), rig.Budgets.Used(goal, rig.Clock.Now));
    }
}
