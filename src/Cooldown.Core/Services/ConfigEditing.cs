using Cooldown.Core.Models;

namespace Cooldown.Core.Services;

/// <summary>
/// Pure helpers for editing config collections. Always return a new list/dictionary
/// rather than mutating in place, so callers can swap the config's property atomically
/// (see the concurrency note on Tracker) instead of touching the collection the tracker reads.
/// </summary>
public static class ConfigEditing
{
    /// <summary>Moves the bucket at <paramref name="index"/> one place earlier (-1) or later (+1).</summary>
    public static List<Bucket> MoveBucket(IReadOnlyList<Bucket> buckets, int index, int direction)
    {
        int target = index + direction;
        if (index < 0 || index >= buckets.Count || target < 0 || target >= buckets.Count)
            return [.. buckets];

        var result = new List<Bucket>(buckets);
        (result[index], result[target]) = (result[target], result[index]);
        return result;
    }

    /// <summary>Removes a bucket and drops it from any game's assignment list, leaving those games unassigned if it was the last one.</summary>
    public static (List<Bucket> Buckets, Dictionary<int, List<string>> Assignments) RemoveBucket(
        IReadOnlyList<Bucket> buckets, IReadOnlyDictionary<int, List<string>> assignments, string bucketId)
    {
        var newBuckets = buckets.Where(b => b.Id != bucketId).ToList();
        var newAssignments = new Dictionary<int, List<string>>();
        foreach (var (appId, bucketIds) in assignments)
        {
            var remaining = bucketIds.Where(id => id != bucketId).ToList();
            if (remaining.Count > 0)
                newAssignments[appId] = remaining;
        }
        return (newBuckets, newAssignments);
    }

    /// <summary>True if any game is currently assigned to this bucket (used to decide whether to confirm a delete).</summary>
    public static bool HasAssignments(IReadOnlyDictionary<int, List<string>> assignments, string bucketId) =>
        assignments.Values.Any(list => list.Contains(bucketId));

    public static List<Bucket> UpsertBucket(IReadOnlyList<Bucket> buckets, Bucket bucket)
    {
        var result = new List<Bucket>(buckets);
        int index = result.FindIndex(b => b.Id == bucket.Id);
        if (index >= 0)
            result[index] = bucket;
        else
            result.Add(bucket);
        return result;
    }

    /// <summary>Adds or removes one bucket from a game's assignment list, leaving other buckets it's in untouched.</summary>
    public static Dictionary<int, List<string>> SetAssignment(
        IReadOnlyDictionary<int, List<string>> assignments, int appId, string bucketId, bool assigned)
    {
        var result = assignments.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value));
        var current = result.GetValueOrDefault(appId) ?? [];

        if (assigned && !current.Contains(bucketId))
            current = [.. current, bucketId];
        else if (!assigned)
            current = current.Where(id => id != bucketId).ToList();

        if (current.Count > 0)
            result[appId] = current;
        else
            result.Remove(appId);
        return result;
    }
}
