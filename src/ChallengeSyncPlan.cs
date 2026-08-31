namespace prepareBikeParking;

/// <summary>
/// Decides what a refresh should do to one challenge. Pure logic, no network, so
/// the rules that matter can be tested without touching MapRoulette.
///
/// The rules, inherited from the sibling against-interpolation project:
///
///   * A task is only ever closed on <b>primary evidence</b> from this run's
///     fetches - the station now exists in the OSM data, or it has left the GBFS
///     feed. Never because it merely fell out of this run's comparison file. A
///     threshold tweak or a half-empty Overpass response can drop a station from
///     the comparison while it is still perfectly missing from OSM, and closing on
///     that would quietly discard real work.
///   * Only tasks still in created/skipped are touched. Fixed, false positive and
///     too hard are a mapper's judgment and always win.
///   * Tasks we already have are never re-uploaded.
/// </summary>
public static class ChallengeSyncPlan
{
    public sealed record TaskChange(long TaskId, string Key, int NewStatus, string Reason);

    public sealed record Plan(
        IReadOnlyList<string> NewKeys,
        IReadOnlyList<TaskChange> Closures)
    {
        public bool IsEmpty => NewKeys.Count == 0 && Closures.Count == 0;
    }

    /// <summary>
    /// Build the plan for one challenge.
    /// </summary>
    /// <param name="desiredKeys">Keys the current comparison says need a task.</param>
    /// <param name="liveTasks">Tasks MapRoulette currently holds for the challenge.</param>
    /// <param name="resolvedKeys">
    /// Keys with primary evidence that the work is done (e.g. the station now appears
    /// in the OSM fetch). These close as Already Fixed.
    /// </param>
    /// <param name="retiredKeys">
    /// Keys whose subject has left the source data entirely (e.g. the station is gone
    /// from GBFS), so the task no longer describes anything real. These close as Deleted.
    /// </param>
    public static Plan Build(
        IEnumerable<string> desiredKeys,
        IEnumerable<MaprouletteApi.TaskInfo> liveTasks,
        IReadOnlySet<string> resolvedKeys,
        IReadOnlySet<string> retiredKeys)
    {
        var live = liveTasks.ToList();
        var liveKeys = new HashSet<string>(live.Select(t => t.Name), StringComparer.Ordinal);

        // Anything MapRoulette already has a task for is not new, whatever its status:
        // re-uploading a station someone already marked Fixed would resurrect the work.
        var newKeys = desiredKeys
            .Distinct(StringComparer.Ordinal)
            .Where(k => !liveKeys.Contains(k))
            .ToList();

        var closures = new List<TaskChange>();
        foreach (var task in live)
        {
            if (!MaprouletteApi.RefreshableStatuses.Contains(task.Status)) continue;

            if (resolvedKeys.Contains(task.Name))
            {
                closures.Add(new TaskChange(task.Id, task.Name, MaprouletteApi.StatusAlreadyFixed,
                    "present in the current OSM data"));
            }
            else if (retiredKeys.Contains(task.Name))
            {
                closures.Add(new TaskChange(task.Id, task.Name, MaprouletteApi.StatusDeleted,
                    "no longer in the GBFS feed"));
            }
        }

        return new Plan(newKeys, closures);
    }

    /// <summary>
    /// Refuse to act on a fetch that looks truncated.
    ///
    /// A scheduled run has nobody watching it, and Overpass rate-limits cloud
    /// runners - a half-empty response looks exactly like "somebody fixed
    /// everything overnight". Closing hundreds of tasks on that is unrecoverable
    /// by hand, so a suspicious fetch fails the run instead. A red workflow is
    /// cheap; silently shredding a challenge is not.
    /// </summary>
    /// <returns>null when the fetch looks sane, otherwise the reason to abort.</returns>
    public static string? CheckFetchSanity(int osmCount, int gbfsCount, int previousOsmCount, double minRetainedFraction = 0.5)
    {
        if (gbfsCount == 0)
        {
            return "the GBFS feed returned no stations";
        }

        if (osmCount == 0 && previousOsmCount > 0)
        {
            return $"the OSM query returned no stations but {previousOsmCount} were seen last run";
        }

        if (previousOsmCount > 0 && osmCount < previousOsmCount * minRetainedFraction)
        {
            return $"the OSM query returned {osmCount} stations, down from {previousOsmCount} last run " +
                   $"(under {minRetainedFraction:P0} of the previous count)";
        }

        return null;
    }
}
