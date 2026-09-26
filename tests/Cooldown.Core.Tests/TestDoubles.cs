using Cooldown.Core.Abstractions;
using Cooldown.Core.Models;

namespace Cooldown.Core.Tests;

internal sealed class ManualClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset Now { get; private set; } = start;
    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class InMemorySessionStore : ISessionStore
{
    private readonly List<PlaySession> _sessions = [];

    public IReadOnlyList<PlaySession> All => _sessions;

    public PlaySession Begin(int appId, DateTimeOffset at)
    {
        var s = new PlaySession(_sessions.Count + 1, appId, at, at);
        _sessions.Add(s);
        return s;
    }

    public void Extend(long sessionId, DateTimeOffset to)
    {
        int i = _sessions.FindIndex(s => s.Id == sessionId);
        _sessions[i] = _sessions[i] with { End = to };
    }

    public IReadOnlyList<PlaySession> GetSessionsSince(DateTimeOffset since) =>
        _sessions.Where(s => s.End >= since).ToList();
}

internal sealed class StubDetector : IGameDetector
{
    public DetectedGame? Running { get; set; }
    public DetectedGame? GetRunningGame() => Running;
}

internal sealed class RecordingTerminator(StubDetector detector) : IGameTerminator
{
    public List<int> Terminated { get; } = [];

    public Task<bool> TerminateAsync(int appId, TimeSpan gracefulTimeout, CancellationToken ct)
    {
        Terminated.Add(appId);
        detector.Running = null;
        return Task.FromResult(true);
    }
}

internal sealed class RecordingNotifier : INotifier
{
    public List<(string Title, string Message, NotificationLevel Level)> Sent { get; } = [];
    public void Notify(string title, string message, NotificationLevel level) => Sent.Add((title, message, level));
}
