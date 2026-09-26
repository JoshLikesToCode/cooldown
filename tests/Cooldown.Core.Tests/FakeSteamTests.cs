using Cooldown.Platform.Fake;

namespace Cooldown.Core.Tests;

public class FakeSteamTests
{
    [Fact]
    public async Task Plays_steps_in_order_and_skips_ahead_when_closed()
    {
        var clock = new ManualClock(DateTimeOffset.Now);
        var fake = new FakeSteam([new(730, "CS2", 10), new(0, null, 5), new(570, "Dota 2", 10)], clock);

        Assert.Equal(730, fake.GetRunningGame()!.AppId);
        clock.Advance(TimeSpan.FromMinutes(12));
        Assert.Null(fake.GetRunningGame());
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(570, fake.GetRunningGame()!.AppId);

        Assert.True(await fake.TerminateAsync(570, TimeSpan.Zero, default));
        Assert.Null(fake.GetRunningGame());
    }
}
