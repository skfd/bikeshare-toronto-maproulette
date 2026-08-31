using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace prepareBikeParking;

/// <summary>
/// The committed record of which MapRoulette challenge belongs to which system and
/// change type, and which tasks we have already uploaded to it.
///
/// Before this existed every run created a brand new dated challenge, which was
/// tolerable when a human only ran the tool when they meant to work the tasks. On a
/// schedule it produces a new duplicate challenge every week for every station
/// nobody has got to yet. One stable challenge per system per type, refreshed in
/// place, is the fix; this file is what lets a later run find the challenge again.
///
/// Lives beside the system's GeoJSON and is committed, so the repo always reflects
/// the live challenge state.
/// </summary>
public sealed class MaprouletteManifest
{
    public const string FileName = "maproulette_manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    /// <summary>
    /// How many OSM stations the last healthy run saw. The sanity check compares
    /// against it to spot a truncated Overpass response before anything is closed.
    /// </summary>
    [JsonPropertyName("last_osm_count")]
    public int LastOsmCount { get; set; }

    /// <summary>Keyed by change type: "added", "removed", "duplicates".</summary>
    [JsonPropertyName("challenges")]
    public Dictionary<string, ChallengeRecord> Challenges { get; set; } = new();

    public sealed class ChallengeRecord
    {
        [JsonPropertyName("challenge_id")]
        public int ChallengeId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("created_at")]
        public DateTime CreatedAtUtc { get; set; }

        [JsonPropertyName("refreshed_at")]
        public DateTime RefreshedAtUtc { get; set; }

        /// <summary>
        /// Stable per-task keys we have uploaded, in upload order. Used to tell a
        /// genuinely new station from one we already have a task for.
        /// </summary>
        [JsonPropertyName("task_keys")]
        public List<string> TaskKeys { get; set; } = new();
    }

    public static MaprouletteManifest Load(string systemName)
    {
        var path = FileManager.GetSystemFullPath(systemName, FileName);
        if (!File.Exists(path))
        {
            return new MaprouletteManifest();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MaprouletteManifest>(json, JsonOptions) ?? new MaprouletteManifest();
        }
        catch (Exception ex)
        {
            // A corrupt manifest must not silently become "no challenges exist",
            // which would make the next run create duplicates of everything.
            Log.Error(ex, "Manifest for {System} could not be read: {Path}", systemName, path);
            throw new InvalidOperationException(
                $"MapRoulette manifest for {systemName} is unreadable ({path}). " +
                "Fix or delete it deliberately - running on without it would duplicate every challenge.", ex);
        }
    }

    public void Save(string systemName)
    {
        var path = FileManager.GetSystemFullPath(systemName, FileName);
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        Log.Information("Manifest saved for {System}: {Path}", systemName, path);
    }
}
