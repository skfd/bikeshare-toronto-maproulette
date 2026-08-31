using System.Text.Json;
using Serilog;

namespace prepareBikeParking;

/// <summary>
/// Keeps one long-lived MapRoulette challenge per system per change type in step
/// with the current comparison, instead of creating a new dated challenge on every
/// run.
///
/// The old behaviour was survivable by hand - you only ran the tool when you meant
/// to work the tasks. On a weekly schedule it produced a fresh duplicate challenge
/// every week for every station nobody had got to yet, and a mapper working the
/// older one did redundant work.
/// </summary>
public static class MaprouletteSync
{
    /// <summary>One challenge type: where its tasks come from and how it is described.</summary>
    private sealed record ChallengeKind(
        string Key,
        string TitleSuffix,
        string SourceFile,
        string InstructionFile,
        int Difficulty);

    private static readonly ChallengeKind[] Kinds =
    {
        new("added", "Stations missing in OSM", "bikeshare_missing_in_osm.geojson",
            Path.Combine("instructions", "added.md"), 3),
        new("removed", "OSM stations no longer in the feed", "bikeshare_extra_in_osm.geojson",
            Path.Combine("instructions", "removed.md"), 2),
        new("duplicates", "Duplicate ref values", "bikeshare_osm_duplicates.geojson",
            Path.Combine("instructions", "duplicates.md"), 2),
    };

    public sealed record SyncOutcome(int Created, int Closed, IReadOnlyList<string> Notes);

    /// <summary>
    /// Bring every challenge for a system up to date.
    /// </summary>
    /// <param name="osmKeys">Every station key present in this run's OSM fetch.</param>
    /// <param name="gbfsKeys">Every station key present in this run's GBFS fetch.</param>
    public static async Task<SyncOutcome> RefreshAsync(
        BikeShareSystem system,
        IReadOnlySet<string> osmKeys,
        IReadOnlySet<string> gbfsKeys)
    {
        var manifest = MaprouletteManifest.Load(system.Name);
        manifest.ProjectId = system.MaprouletteProjectId;

        var created = 0;
        var closed = 0;
        var notes = new List<string>();

        foreach (var kind in Kinds)
        {
            try
            {
                var (added, shut, note) = await RefreshKindAsync(system, kind, manifest, osmKeys, gbfsKeys);
                created += added;
                closed += shut;
                if (note != null) notes.Add(note);
            }
            catch (Exception ex)
            {
                // One challenge type failing must not abandon the others, nor lose
                // the manifest updates already made for them.
                Log.Error(ex, "Refresh failed for {System} / {Kind}", system.Name, kind.Key);
                notes.Add($"{kind.Key}: refresh failed - {ex.Message}");
            }
        }

        manifest.Save(system.Name);
        return new SyncOutcome(created, closed, notes);
    }

    private static async Task<(int Created, int Closed, string? Note)> RefreshKindAsync(
        BikeShareSystem system,
        ChallengeKind kind,
        MaprouletteManifest manifest,
        IReadOnlySet<string> osmKeys,
        IReadOnlySet<string> gbfsKeys)
    {
        var sourcePath = FileManager.GetSystemFullPath(system.Name, kind.SourceFile);
        var hasSource = File.Exists(sourcePath);
        var entries = hasSource ? ReadEntries(sourcePath) : new List<(string Key, string Line)>();

        manifest.Challenges.TryGetValue(kind.Key, out var record);

        // Nothing to do and nothing to maintain: don't create an empty challenge.
        if (entries.Count == 0 && record == null)
        {
            return (0, 0, null);
        }

        var challengeName = $"{system.Name} — {kind.TitleSuffix}";

        if (record == null)
        {
            // The manifest may simply have been lost; adopt an existing challenge of
            // the same name rather than creating a second one beside it.
            var existing = await MaprouletteApi.FindChallengeByNameAsync(system.MaprouletteProjectId, challengeName);
            var instruction = await ReadInstructionAsync(system.Name, kind);

            var challengeId = existing ?? await MaprouletteApi.CreateChallengeAsync(
                system.MaprouletteProjectId,
                challengeName,
                instruction,
                $"{kind.TitleSuffix} for {system.Name} #maproulette",
                kind.Difficulty);

            record = new MaprouletteManifest.ChallengeRecord
            {
                ChallengeId = challengeId,
                Name = challengeName,
                CreatedAtUtc = DateTime.UtcNow,
            };
            manifest.Challenges[kind.Key] = record;

            if (existing != null)
            {
                Log.Information("Adopted existing challenge {Id} for {Name}", existing, challengeName);
            }
        }

        var liveTasks = await MaprouletteApi.GetTasksAsync(record.ChallengeId);

        var (resolved, retired) = Evidence(kind.Key, liveTasks, osmKeys, gbfsKeys);
        var plan = ChallengeSyncPlan.Build(entries.Select(e => e.Key), liveTasks, resolved, retired);

        if (plan.NewKeys.Count > 0)
        {
            var byKey = entries.ToDictionary(e => e.Key, e => e.Line, StringComparer.Ordinal);
            var upload = plan.NewKeys
                .Where(byKey.ContainsKey)
                .Select(k => (Key: k, Line: byKey[k]))
                .ToList();

            await MaprouletteApi.AddTasksAsync(record.ChallengeId, upload);
            foreach (var (key, _) in upload)
            {
                if (!record.TaskKeys.Contains(key)) record.TaskKeys.Add(key);
            }
        }

        foreach (var closure in plan.Closures)
        {
            await MaprouletteApi.SetTaskStatusAsync(closure.TaskId, closure.NewStatus);
            Log.Information("Closed task {TaskId} ({Key}) in {Challenge}: {Reason}",
                closure.TaskId, closure.Key, record.Name, closure.Reason);
        }

        record.RefreshedAtUtc = DateTime.UtcNow;

        var note = plan.IsEmpty
            ? null
            : $"{challengeName}: +{plan.NewKeys.Count} new, {plan.Closures.Count} closed";

        return (plan.NewKeys.Count, plan.Closures.Count, note);
    }

    /// <summary>
    /// Which live tasks have primary evidence that they are finished or moot.
    /// Deliberately derived from this run's fetches, never from whether the key is
    /// still in the comparison file.
    /// </summary>
    private static (IReadOnlySet<string> Resolved, IReadOnlySet<string> Retired) Evidence(
        string kindKey,
        IReadOnlyList<MaprouletteApi.TaskInfo> liveTasks,
        IReadOnlySet<string> osmKeys,
        IReadOnlySet<string> gbfsKeys)
    {
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        var retired = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in liveTasks)
        {
            var key = task.Name;
            if (string.IsNullOrEmpty(key)) continue;

            switch (kindKey)
            {
                case "added":
                    // "This station is missing from OSM."
                    if (osmKeys.Contains(key)) resolved.Add(key);       // somebody mapped it
                    else if (!gbfsKeys.Contains(key)) retired.Add(key); // the station is gone
                    break;

                case "removed":
                    // "This OSM station is not in the feed any more."
                    if (!osmKeys.Contains(key)) resolved.Add(key);      // it was removed from OSM
                    else if (gbfsKeys.Contains(key)) retired.Add(key);  // the station came back
                    break;

                case "duplicates":
                    // Only closable on the node disappearing from OSM; whether a ref is
                    // still duplicated is judged by the detector, not here.
                    if (!osmKeys.Contains(key)) resolved.Add(key);
                    break;
            }
        }

        return (resolved, retired);
    }

    private static async Task<string> ReadInstructionAsync(string systemName, ChallengeKind kind)
    {
        if (!FileManager.SystemFileExists(systemName, kind.InstructionFile))
        {
            throw new FileNotFoundException(
                $"Instruction file missing: {FileManager.GetSystemFilePath(systemName, kind.InstructionFile)}. " +
                $"Cannot create the {kind.Key} challenge without it.");
        }

        var instruction = await FileManager.ReadSystemTextFileAsync(systemName, kind.InstructionFile);
        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new InvalidOperationException(
                $"Instruction file is empty: {FileManager.GetSystemFilePath(systemName, kind.InstructionFile)}.");
        }

        return instruction;
    }

    private static List<(string Key, string Line)> ReadEntries(string path)
    {
        var entries = new List<(string, string)>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.TrimStart('').Trim();
            if (line.Length == 0) continue;

            var key = TaskKey(line);
            if (key != null) entries.Add((key, line));
        }
        return entries;
    }

    /// <summary>
    /// The stable identity of a task's subject.
    ///
    /// An OSM object identifies itself; otherwise the GBFS station id (carried, for
    /// historical reasons, in the "address" property) is the key. The duplicates
    /// file needs the OSM form specifically - refs repeat there by definition, so a
    /// ref alone would not be unique within that challenge.
    /// </summary>
    internal static string? TaskKey(string featureCollectionJson)
    {
        try
        {
            var props = JsonDocument.Parse(featureCollectionJson)
                .RootElement.GetProperty("features")[0]
                .GetProperty("properties");

            if (props.TryGetProperty("osmType", out var type) &&
                props.TryGetProperty("osmId", out var id))
            {
                var t = type.GetString();
                var i = id.GetString();
                if (!string.IsNullOrEmpty(t) && !string.IsNullOrEmpty(i)) return $"{t}/{i}";
            }

            if (props.TryGetProperty("address", out var address))
            {
                var value = address.GetString();
                if (!string.IsNullOrEmpty(value)) return value;
            }

            // An OSM station with no ref at all still deserves a task, so fall back
            // to its position rather than dropping it silently.
            if (props.TryGetProperty("latitude", out var lat) && props.TryGetProperty("longitude", out var lon))
            {
                return $"@{lat.GetString()},{lon.GetString()}";
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not derive a task key from a GeoJSON line");
        }

        return null;
    }
}
