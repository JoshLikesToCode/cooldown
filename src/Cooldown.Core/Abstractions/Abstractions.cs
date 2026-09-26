using Cooldown.Core.Models;

namespace Cooldown.Core.Abstractions;

/// <summary>Reports which Steam game is running right now, if any.</summary>
public interface IGameDetector
{
    DetectedGame? GetRunningGame();
}

/// <summary>Closes a running game. Tries a graceful close before force-killing.</summary>
public interface IGameTerminator
{
    Task<bool> TerminateAsync(int appId, TimeSpan gracefulTimeout, CancellationToken ct);
}

public interface INotifier
{
    void Notify(string title, string message, NotificationLevel level);
}

public interface IClock
{
    DateTimeOffset Now { get; }

    /// <summary>How fast this clock runs relative to real time. Only the fake clock changes this.</summary>
    double Speed => 1.0;
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

public interface ISessionStore
{
    PlaySession Begin(int appId, DateTimeOffset at);
    void Extend(long sessionId, DateTimeOffset to);

    /// <summary>All sessions that ended at or after <paramref name="since"/>.</summary>
    IReadOnlyList<PlaySession> GetSessionsSince(DateTimeOffset since);
}
