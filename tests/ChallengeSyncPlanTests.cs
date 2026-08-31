using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace prepareBikeParking.Tests;

/// <summary>
/// The refresh rules that keep a scheduled run from destroying mapper work.
/// These are the reason challenges are long-lived rather than recreated weekly,
/// so they are worth pinning down hard.
/// </summary>
public class ChallengeSyncPlanTests
{
    private static MaprouletteApi.TaskInfo Task(long id, string key, int status) => new(id, key, status);

    private static IReadOnlySet<string> Set(params string[] keys) =>
        new HashSet<string>(keys, System.StringComparer.Ordinal);

    [Test]
    public void UploadsOnlyStationsWithoutATaskAlready()
    {
        var plan = ChallengeSyncPlan.Build(
            desiredKeys: new[] { "7001", "7002", "7003" },
            liveTasks: new[] { Task(1, "7001", MaprouletteApi.StatusCreated) },
            resolvedKeys: Set(),
            retiredKeys: Set());

        Assert.That(plan.NewKeys, Is.EquivalentTo(new[] { "7002", "7003" }));
    }

    [Test]
    public void NobodyCompletingTheTasksProducesNoDuplicates()
    {
        // The exact weekly-cron scenario: same stations still missing, tasks
        // untouched. A second run must add nothing at all.
        var desired = new[] { "7001", "7002" };
        var live = new[]
        {
            Task(1, "7001", MaprouletteApi.StatusCreated),
            Task(2, "7002", MaprouletteApi.StatusCreated),
        };

        var plan = ChallengeSyncPlan.Build(desired, live, Set(), Set());

        Assert.That(plan.NewKeys, Is.Empty);
        Assert.That(plan.Closures, Is.Empty);
        Assert.That(plan.IsEmpty, Is.True);
    }

    [Test]
    public void DoesNotReuploadAStationAMapperAlreadyFixed()
    {
        // Still "missing" per the file (OSM data can lag), but the task exists and
        // is Fixed. Re-uploading would resurrect finished work as a new task.
        var plan = ChallengeSyncPlan.Build(
            desiredKeys: new[] { "7001" },
            liveTasks: new[] { Task(1, "7001", MaprouletteApi.StatusFixed) },
            resolvedKeys: Set(),
            retiredKeys: Set());

        Assert.That(plan.NewKeys, Is.Empty);
    }

    [Test]
    public void ClosesATaskOnceTheStationAppearsInOsm()
    {
        var plan = ChallengeSyncPlan.Build(
            desiredKeys: System.Array.Empty<string>(),
            liveTasks: new[] { Task(9, "7001", MaprouletteApi.StatusCreated) },
            resolvedKeys: Set("7001"),
            retiredKeys: Set());

        Assert.That(plan.Closures, Has.Count.EqualTo(1));
        Assert.That(plan.Closures[0].TaskId, Is.EqualTo(9));
        Assert.That(plan.Closures[0].NewStatus, Is.EqualTo(MaprouletteApi.StatusAlreadyFixed));
    }

    [Test]
    public void ClosesATaskAsDeletedWhenTheStationLeavesTheFeed()
    {
        var plan = ChallengeSyncPlan.Build(
            desiredKeys: System.Array.Empty<string>(),
            liveTasks: new[] { Task(9, "7001", MaprouletteApi.StatusCreated) },
            resolvedKeys: Set(),
            retiredKeys: Set("7001"));

        Assert.That(plan.Closures.Single().NewStatus, Is.EqualTo(MaprouletteApi.StatusDeleted));
    }

    [Test]
    public void NeverOverwritesAMapperJudgment()
    {
        // Even with evidence the work is done, Fixed / Not an Issue / Too Difficult
        // are the mapper's call and must survive a refresh untouched.
        var live = new[]
        {
            Task(1, "a", MaprouletteApi.StatusFixed),
            Task(2, "b", MaprouletteApi.StatusFalsePositive),
            Task(3, "c", MaprouletteApi.StatusTooHard),
            Task(4, "d", MaprouletteApi.StatusAlreadyFixed),
        };

        var plan = ChallengeSyncPlan.Build(
            desiredKeys: System.Array.Empty<string>(),
            liveTasks: live,
            resolvedKeys: Set("a", "b", "c", "d"),
            retiredKeys: Set());

        Assert.That(plan.Closures, Is.Empty);
    }

    [Test]
    public void SkippedTasksMayBeClosed()
    {
        var plan = ChallengeSyncPlan.Build(
            desiredKeys: System.Array.Empty<string>(),
            liveTasks: new[] { Task(1, "a", MaprouletteApi.StatusSkipped) },
            resolvedKeys: Set("a"),
            retiredKeys: Set());

        Assert.That(plan.Closures, Has.Count.EqualTo(1));
    }

    [Test]
    public void DoesNotCloseMerelyBecauseAStationLeftTheComparisonFile()
    {
        // The rule inherited from against-interpolation: absence from this run's
        // file is not evidence. A threshold tweak can drop a station while it is
        // still perfectly missing from OSM.
        var plan = ChallengeSyncPlan.Build(
            desiredKeys: System.Array.Empty<string>(),
            liveTasks: new[] { Task(1, "7001", MaprouletteApi.StatusCreated) },
            resolvedKeys: Set(),
            retiredKeys: Set());

        Assert.That(plan.Closures, Is.Empty);
    }

    [Test]
    public void PartialCompletionAddsOnlyTheGenuinelyNewStations()
    {
        // 5 of the original 12 got fixed; 3 new stations appeared this week.
        var live = Enumerable.Range(1, 12)
            .Select(i => Task(i, $"s{i}", i <= 5 ? MaprouletteApi.StatusFixed : MaprouletteApi.StatusCreated))
            .ToArray();

        var desired = Enumerable.Range(6, 7).Select(i => $"s{i}")
            .Concat(new[] { "new1", "new2", "new3" });

        var plan = ChallengeSyncPlan.Build(desired, live, Set("s1", "s2", "s3", "s4", "s5"), Set());

        Assert.That(plan.NewKeys, Is.EquivalentTo(new[] { "new1", "new2", "new3" }));
        Assert.That(plan.Closures, Is.Empty, "the five fixed tasks are already closed by the mapper");
    }

    [Test]
    public void PositionKeyedTasksAreNeverClosed()
    {
        // A ref-less OSM station is keyed by position, and position keys can never
        // appear in the OSM key set (which holds refs and object ids only). Closing
        // on their absence would retire every such task on its first refresh, and it
        // could never come back: the task exists, so it is never re-uploaded.
        var plan = ChallengeSyncPlan.Build(
            desiredKeys: System.Array.Empty<string>(),
            liveTasks: new[] { Task(1, "@43.65,-79.4", MaprouletteApi.StatusCreated) },
            resolvedKeys: Set("@43.65,-79.4"),
            retiredKeys: Set("@43.65,-79.4"));

        Assert.That(plan.Closures, Is.Empty);
    }

    [Test]
    public void EmptyGbfsFetchAbortsTheSync()
    {
        Assert.That(ChallengeSyncPlan.CheckFetchSanity(osmCount: 100, gbfsCount: 0, previousOsmCount: 100),
            Is.Not.Null);
    }

    [Test]
    public void EmptyOsmFetchAbortsWhenWePreviouslySawStations()
    {
        Assert.That(ChallengeSyncPlan.CheckFetchSanity(osmCount: 0, gbfsCount: 700, previousOsmCount: 680),
            Is.Not.Null);
    }

    [Test]
    public void HalvedOsmFetchAbortsTheSync()
    {
        // A truncated Overpass response looks exactly like mass deletion.
        Assert.That(ChallengeSyncPlan.CheckFetchSanity(osmCount: 100, gbfsCount: 700, previousOsmCount: 680),
            Is.Not.Null);
    }

    [Test]
    public void NormalFluctuationPassesTheSanityCheck()
    {
        Assert.That(ChallengeSyncPlan.CheckFetchSanity(osmCount: 675, gbfsCount: 700, previousOsmCount: 680),
            Is.Null);
    }

    [Test]
    public void FirstEverRunHasNoPreviousCountAndIsAllowed()
    {
        Assert.That(ChallengeSyncPlan.CheckFetchSanity(osmCount: 0, gbfsCount: 700, previousOsmCount: 0),
            Is.Null);
    }
}
