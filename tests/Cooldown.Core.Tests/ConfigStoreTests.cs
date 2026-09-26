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
        created.Assignments[730] = "competitive";
        ConfigStore.Save(path, created);

        var loaded = ConfigStore.LoadOrCreate(path, () => throw new InvalidOperationException("should load"));

        Assert.True(created.Buckets.SequenceEqual(loaded.Buckets));
        Assert.Equal("competitive", loaded.Assignments[730]);
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
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
