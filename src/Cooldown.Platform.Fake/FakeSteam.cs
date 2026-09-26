using System.Text.Json;
using Cooldown.Core;
using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;

namespace Cooldown.Platform.Fake;

/// <summary>One step of a scripted play session. AppId 0 means nothing is running.</summary>
public sealed record FakeStep(int AppId, string? Name, int Minutes);

/// <summary>
/// Plays back a scripted list of "games" so the whole app can run on a machine without Steam.
/// Terminating the current game skips to the next step, like a real close would.
/// </summary>
public sealed class FakeSteam(IReadOnlyList<FakeStep> steps, IClock clock) : IGameDetector, IGameTerminator
{
    private readonly Lock _gate = new();
    private int _index;
    private DateTimeOffset _stepStarted = clock.Now;

    public static FakeSteam FromFile(string path, IClock clock)
    {
        var steps = JsonSerializer.Deserialize<List<FakeStep>>(File.ReadAllText(path), JsonOptions)
                    ?? throw new InvalidDataException($"{path} is empty.");
        return new FakeSteam(steps, clock);
    }

    public static FakeSteam Default(IClock clock) => new(
    [
        new(730, "Counter-Strike 2", 50),
        new(0, null, 5),
        new(730, "Counter-Strike 2", 30),
        new(1145360, "Hades", 120),
    ], clock);

    public DetectedGame? GetRunningGame()
    {
        lock (_gate)
        {
            var now = clock.Now;
            while (_index < steps.Count && now - _stepStarted >= TimeSpan.FromMinutes(steps[_index].Minutes))
            {
                _stepStarted += TimeSpan.FromMinutes(steps[_index].Minutes);
                _index++;
            }

            if (_index >= steps.Count || steps[_index].AppId == 0)
                return null;
            return new DetectedGame(steps[_index].AppId, steps[_index].Name);
        }
    }

    public Task<bool> TerminateAsync(int appId, TimeSpan gracefulTimeout, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_index >= steps.Count || steps[_index].AppId != appId)
                return Task.FromResult(false);

            Log.Info($"[fake] Closed {steps[_index].Name}");
            _index++;
            _stepStarted = clock.Now;
            return Task.FromResult(true);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
}

/// <summary>A clock that runs faster than real time, so a one-hour budget can be tested in a minute.</summary>
public sealed class ScaledClock(double speed) : IClock
{
    private readonly DateTimeOffset _realStart = DateTimeOffset.Now;

    public double Speed => speed;

    public DateTimeOffset Now => _realStart + (DateTimeOffset.Now - _realStart) * speed;
}
