using Cooldown.Core.Models;
using Cooldown.Core.Services;

namespace Cooldown.Core.Tests;

public class ConfigEditingTests
{
    private static readonly Bucket A = new("a", "A", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Block);
    private static readonly Bucket B = new("b", "B", TimeSpan.FromHours(1), ResetPeriod.Daily, Enforcement.Remind);
    private static readonly Bucket C = new("c", "C", TimeSpan.FromHours(1), ResetPeriod.Weekly, Enforcement.Block);

    [Fact]
    public void MoveBucket_swaps_with_the_neighbor_in_the_given_direction()
    {
        var result = ConfigEditing.MoveBucket([A, B, C], 0, +1);
        Assert.Equal(["b", "a", "c"], result.Select(b => b.Id));
    }

    [Fact]
    public void MoveBucket_is_a_noop_past_either_end()
    {
        Assert.Equal(["a", "b", "c"], ConfigEditing.MoveBucket([A, B, C], 0, -1).Select(b => b.Id));
        Assert.Equal(["a", "b", "c"], ConfigEditing.MoveBucket([A, B, C], 2, +1).Select(b => b.Id));
    }

    [Fact]
    public void RemoveBucket_unassigns_games_instead_of_leaving_dangling_references()
    {
        var assignments = new Dictionary<int, List<string>> { [730] = ["a"], [570] = ["b"] };
        var (buckets, newAssignments) = ConfigEditing.RemoveBucket([A, B], assignments, "a");

        Assert.DoesNotContain(buckets, b => b.Id == "a");
        Assert.False(newAssignments.ContainsKey(730));
        Assert.Equal(["b"], newAssignments[570]);
    }

    [Fact]
    public void RemoveBucket_only_drops_the_removed_id_from_a_multi_bucket_assignment()
    {
        var assignments = new Dictionary<int, List<string>> { [730] = ["a", "b"] };
        var (_, newAssignments) = ConfigEditing.RemoveBucket([A, B], assignments, "a");

        Assert.Equal(["b"], newAssignments[730]);
    }

    [Fact]
    public void HasAssignments_reflects_current_assignments()
    {
        var assignments = new Dictionary<int, List<string>> { [730] = ["a"] };
        Assert.True(ConfigEditing.HasAssignments(assignments, "a"));
        Assert.False(ConfigEditing.HasAssignments(assignments, "b"));
    }

    [Fact]
    public void UpsertBucket_replaces_an_existing_id_and_appends_a_new_one()
    {
        var replaced = ConfigEditing.UpsertBucket([A, B], A with { Name = "Renamed" });
        Assert.Equal("Renamed", replaced.Single(b => b.Id == "a").Name);
        Assert.Equal(2, replaced.Count);

        var appended = ConfigEditing.UpsertBucket([A, B], C);
        Assert.Equal(3, appended.Count);
    }

    [Fact]
    public void SetAssignment_adds_and_removes_a_single_bucket_without_touching_others()
    {
        var assignments = new Dictionary<int, List<string>> { [730] = ["a"] };

        var added = ConfigEditing.SetAssignment(assignments, 730, "b", assigned: true);
        Assert.Equal(["a", "b"], added[730]);

        var removed = ConfigEditing.SetAssignment(added, 730, "a", assigned: false);
        Assert.Equal(["b"], removed[730]);

        var clearedToEmpty = ConfigEditing.SetAssignment(removed, 730, "b", assigned: false);
        Assert.False(clearedToEmpty.ContainsKey(730));
    }
}
