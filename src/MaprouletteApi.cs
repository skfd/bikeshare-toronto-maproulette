using System.Text;
using System.Text.Json;
using Serilog;

namespace prepareBikeParking;

/// <summary>
/// The MapRoulette endpoints needed to keep a challenge alive across runs, rather
/// than creating a fresh one each time.
///
/// Mechanics mirror the sibling against-interpolation project, which has been
/// running these calls weekly: addTasks parses a single JSON value per request, so
/// tasks go up one FeatureCollection at a time with the U+001E record separator
/// stripped; task listing is paged.
/// </summary>
public static class MaprouletteApi
{
    /// <summary>GeoJSON lines in this repo are record-separated; MapRoulette wants the JSON alone.</summary>
    private const char RecordSeparator = '';

    public const string ApiBase = "https://maproulette.org/api/v2";

    // MapRoulette's fixed task status enum.
    public const int StatusCreated = 0;
    public const int StatusFixed = 1;
    public const int StatusFalsePositive = 2;
    public const int StatusSkipped = 3;
    public const int StatusDeleted = 4;
    public const int StatusAlreadyFixed = 5;
    public const int StatusTooHard = 6;

    /// <summary>
    /// The only statuses a refresh may overwrite. Everything else is a mapper's
    /// judgment - if someone marked a task Fixed, Not an Issue or Too Difficult,
    /// an automated run has no business reversing it.
    /// </summary>
    public static readonly HashSet<int> RefreshableStatuses = new() { StatusCreated, StatusSkipped };

    public sealed record TaskInfo(long Id, string Name, int Status);

    private static HttpClient Client()
    {
        var apiKey = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("MAPROULETTE_API_KEY environment variable is not set.");
        }

        var client = MaprouletteTaskCreator.HttpFactory.CreateClient();
        client.DefaultRequestHeaders.Remove("apiKey");
        client.DefaultRequestHeaders.Add("apiKey", apiKey);
        return client;
    }

    /// <summary>
    /// Find a challenge by exact name under a project. Lets a run recover the
    /// challenge when the manifest was lost, instead of creating a duplicate.
    /// </summary>
    public static async Task<int?> FindChallengeByNameAsync(int projectId, string name)
    {
        var client = Client();
        var response = await client.GetAsync($"{ApiBase}/project/{projectId}/challenges?limit=1000");
        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("Could not list challenges for project {ProjectId}: {Status}", projectId, response.StatusCode);
            return null;
        }

        var challenges = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        if (challenges.ValueKind != JsonValueKind.Array) return null;

        foreach (var challenge in challenges.EnumerateArray())
        {
            if (challenge.TryGetProperty("name", out var n) &&
                string.Equals(n.GetString(), name, StringComparison.Ordinal) &&
                challenge.TryGetProperty("id", out var id))
            {
                return id.GetInt32();
            }
        }

        return null;
    }

    public static async Task<int> CreateChallengeAsync(int projectId, string name, string instruction, string checkinComment, int difficulty)
    {
        var client = Client();
        var payload = new
        {
            name,
            description = name,
            instruction,
            blurb = instruction,
            checkinComment,
            enabled = true,
            difficulty,
            requiresLocal = false,
            parent = projectId
        };

        var response = await client.PostAsync(
            $"{ApiBase}/challenge",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to create challenge '{name}' ({(int)response.StatusCode}): {body}");
        }

        var id = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("id").GetInt32();
        Log.Information("Created challenge {Name} (id {Id})", name, id);
        return id;
    }

    /// <summary>Every task in the challenge, paged.</summary>
    public static async Task<List<TaskInfo>> GetTasksAsync(int challengeId)
    {
        var client = Client();
        var tasks = new List<TaskInfo>();
        const int limit = 1000;
        var page = 0;

        while (true)
        {
            var response = await client.GetAsync($"{ApiBase}/challenge/{challengeId}/tasks?limit={limit}&page={page}");
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to list tasks for challenge {challengeId} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
            }

            var batch = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            if (batch.ValueKind != JsonValueKind.Array) break;

            var count = 0;
            foreach (var task in batch.EnumerateArray())
            {
                count++;
                tasks.Add(new TaskInfo(
                    task.GetProperty("id").GetInt64(),
                    task.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    task.TryGetProperty("status", out var s) ? s.GetInt32() : StatusCreated));
            }

            if (count < limit) break;
            page++;
        }

        return tasks;
    }

    /// <summary>
    /// Upload tasks, one FeatureCollection per request. The record separator is
    /// stripped: addTasks rejects a body that is not a single JSON value.
    ///
    /// Each feature gets an "@id" property carrying its stable key. MapRoulette
    /// names the task after it, and that name is what lets a later refresh
    /// recognise the task as already covering a station instead of uploading a
    /// duplicate.
    /// </summary>
    public static async Task AddTasksAsync(int challengeId, IReadOnlyList<(string Key, string Line)> tasks)
    {
        var client = Client();
        var url = $"{ApiBase}/challenge/{challengeId}/addTasks";
        var uploaded = 0;

        foreach (var (key, line) in tasks)
        {
            var body = WithStableId(line.TrimStart(RecordSeparator).Trim(), key);
            if (body.Length == 0) continue;

            var response = await client.PutAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"addTasks failed on task {uploaded + 1}/{tasks.Count} for challenge {challengeId} " +
                    $"({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
            }

            uploaded++;
            await Task.Delay(100);
        }

        Log.Information("Uploaded {Count} task(s) to challenge {ChallengeId}", uploaded, challengeId);
    }

    /// <summary>
    /// Add "@id": key to every feature's properties. Done at upload time rather
    /// than in the generated files so the committed GeoJSON keeps its current
    /// shape and the baselines do not all churn.
    /// </summary>
    internal static string WithStableId(string featureCollectionJson, string key)
    {
        if (string.IsNullOrWhiteSpace(featureCollectionJson)) return "";

        var root = System.Text.Json.Nodes.JsonNode.Parse(featureCollectionJson)?.AsObject();
        if (root?["features"] is not System.Text.Json.Nodes.JsonArray features) return featureCollectionJson;

        foreach (var feature in features)
        {
            if (feature?["properties"] is System.Text.Json.Nodes.JsonObject props)
            {
                props["@id"] = key;
            }
        }

        return root.ToJsonString();
    }

    public static async Task SetTaskStatusAsync(long taskId, int status)
    {
        var client = Client();
        var response = await client.PutAsync($"{ApiBase}/task/{taskId}/{status}", null);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to set status {status} on task {taskId} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
        }

        await Task.Delay(100);
    }
}
