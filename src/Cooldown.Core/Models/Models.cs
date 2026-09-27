namespace Cooldown.Core.Models;

public enum ResetPeriod { Daily, Weekly, Monthly }

/// <summary>Remind = notify only. Block = close the game after a grace period.</summary>
public enum Enforcement { Remind, Block }

public enum NotificationLevel { Info, Warning, Alert }

public sealed record Bucket(
    string Id,
    string Name,
    TimeSpan Budget,
    ResetPeriod Period,
    Enforcement Enforcement,
    /// <summary>Hex color like "#7FD1F5", or null to use the default remaining-time coloring.</summary>
    string? Color = null,
    /// <summary>Short emoji/text shown next to the bucket name, or null for none.</summary>
    string? Icon = null,
    /// <summary>
    /// Goal buckets count play time UP toward Budget instead of counting a limit DOWN from it.
    /// They're never enforced (no warnings, no closing) - just tracked and shown as progress.
    /// </summary>
    bool IsGoal = false);

public sealed record DetectedGame(int AppId, string? Name)
{
    public string DisplayName => Name ?? $"App {AppId}";
}

public sealed record PlaySession(long Id, int AppId, DateTimeOffset Start, DateTimeOffset End);

public sealed record BucketStatus(Bucket Bucket, TimeSpan Used, TimeSpan Remaining)
{
    public bool IsExhausted => Remaining <= TimeSpan.Zero;
}
