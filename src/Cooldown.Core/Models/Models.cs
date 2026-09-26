namespace Cooldown.Core.Models;

public enum ResetPeriod { Daily, Weekly }

/// <summary>Remind = notify only. Block = close the game after a grace period.</summary>
public enum Enforcement { Remind, Block }

public enum NotificationLevel { Info, Warning, Alert }

public sealed record Bucket(
    string Id,
    string Name,
    TimeSpan Budget,
    ResetPeriod Period,
    Enforcement Enforcement);

public sealed record DetectedGame(int AppId, string? Name)
{
    public string DisplayName => Name ?? $"App {AppId}";
}

public sealed record PlaySession(long Id, int AppId, DateTimeOffset Start, DateTimeOffset End);

public sealed record BucketStatus(Bucket Bucket, TimeSpan Used, TimeSpan Remaining)
{
    public bool IsExhausted => Remaining <= TimeSpan.Zero;
}
