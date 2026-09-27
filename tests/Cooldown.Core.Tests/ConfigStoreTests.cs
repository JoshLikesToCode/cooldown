using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.Core.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cooldown-config-").FullName;

    [Fact]
    public void Creates_default_on_first_run_and_round_trips()
    {
        var path = Path.Combine(_dir, "config.json");
        var created = ConfigStore.LoadOrCreate(path, CooldownConfig.CreateDefault);
        created.Assignments[730] = ["competitive"];
        ConfigStore.Save(path, created);

        var loaded = ConfigStore.LoadOrCreate(path, () => throw new InvalidOperationException("should load"));

        Assert.True(created.Buckets.SequenceEqual(loaded.Buckets));
        Assert.Equal(["competitive"], loaded.Assignments[730]);
    }

    [Fact]
    public void Reads_hand_written_json()
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, """
            {
              "buckets": [
                { "id": "mp", "name": "Multiplayer", "budget": "01:30:00", "period": "Daily", "enforcement": "Block" }
              ],
              "assignments": { "730": "mp" }
            }
            """);

        var config = ConfigStore.LoadOrCreate(path, CooldownConfig.CreateDefault);
        var bucket = Assert.Single(config.Buckets);
        Assert.Equal(TimeSpan.FromMinutes(90), bucket.Budget);
        Assert.Equal(Enforcement.Block, bucket.Enforcement);
        Assert.Equal(5, config.PollSeconds); // unspecified settings keep their defaults
        Assert.Equal(["mp"], config.Assignments[730]); // old single-bucket-per-game shape upgrades automatically
    }

    [Fact]
    public void Reads_the_multi_bucket_assignment_shape()
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, """
            {
              "buckets": [
                { "id": "mp", "name": "Multiplayer", "budget": "01:00:00", "period": "Daily", "enforcement": "Block" },
                { "id": "learning", "name": "Learning", "budget": "02:00:00", "period": "Monthly", "enforcement": "Remind", "isGoal": true }
              ],
              "assignments": { "730": ["mp", "learning"] }
            }
            """);

        var config = ConfigStore.LoadOrCreate(path, CooldownConfig.CreateDefault);
        Assert.Equal(["mp", "learning"], config.Assignments[730]);
        Assert.True(config.Buckets.Single(b => b.Id == "learning").IsGoal);
    }

    [Fact]
    public void Bucket_color_and_icon_default_to_null_on_old_configs()
    {
        var path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, """
            {
              "buckets": [
                { "id": "mp", "name": "Multiplayer", "budget": "01:30:00", "period": "Daily", "enforcement": "Block" }
              ]
            }
            """);

        var bucket = Assert.Single(ConfigStore.LoadOrCreate(path, CooldownConfig.CreateDefault).Buckets);
        Assert.Null(bucket.Color);
        Assert.Null(bucket.Icon);
    }

    [Fact]
    public void Bucket_color_and_icon_round_trip()
    {
        var path = Path.Combine(_dir, "config.json");
        var config = new CooldownConfig
        {
            Buckets = [new("mp", "Multiplayer", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Block, "#7FD1F5", "🎮")],
        };
        ConfigStore.Save(path, config);

        var loaded = ConfigStore.LoadOrCreate(path, () => throw new InvalidOperationException("should load"));
        var bucket = Assert.Single(loaded.Buckets);
        Assert.Equal("#7FD1F5", bucket.Color);
        Assert.Equal("🎮", bucket.Icon);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
