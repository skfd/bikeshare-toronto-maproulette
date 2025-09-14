using System.Text;
using System.Text.Json;
using Serilog;

namespace prepareBikeParking
{
    public interface IMaprouletteHttpClientFactory
    {
        HttpClient CreateClient();
    }

    public class DefaultMaprouletteHttpClientFactory : IMaprouletteHttpClientFactory
    {
        public HttpClient CreateClient() => new HttpClient();
    }

    public static class MaprouletteTaskCreator
    {
        internal static IMaprouletteHttpClientFactory HttpFactory { get; set; } = new DefaultMaprouletteHttpClientFactory();
        public async static Task CreateTasksAsync(int projectId, DateTime lastSyncDate, string systemName = "Toronto", bool isNewSystem = false)
        {
            Serilog.Log.Information("Creating Maproulette tasks...");

            // First, validate that the project exists and is accessible
            if (!await ValidateProjectExistsAsync(projectId))
            {
                throw new InvalidOperationException($"Maproulette project validation failed for project ID {projectId}. Cannot proceed with task creation.");
            }

            Serilog.Log.Information("Maproulette project {ProjectId} validated successfully", projectId);

            if (isNewSystem)
            {
                Serilog.Log.Information("New system setup detected; skipping 'removed' task creation to preserve existing OSM data.");
                Serilog.Log.Information("Only 'added' (and future 'moved') tasks will be created in this run.");
            }

            // Create challenges for each type of change
            // Skip 'removed' tasks for new systems to avoid deleting existing OSM stations
            if (!isNewSystem)
            {
                await CreateTaskForTypeAsync(projectId, "removed", $"{systemName} -- Removed stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                    Path.Combine("instructions", "removed.md"), systemName, "bikeshare_extra_in_osm.geojson");
            }
            else
            {
                Serilog.Log.Information("Skipping 'removed' challenge creation for new system.");
            }

            await CreateTaskForTypeAsync(projectId, "added", $"{systemName} -- Added stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                Path.Combine("instructions", "added.md"), systemName, "bikeshare_missing_in_osm.geojson");

            Serilog.Log.Information("'moved' tasks currently disabled (logic pending refinement)");
            //await CreateTaskForTypeAsync(projectId, "moved", $"{systemName} -- Moved stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
            //    Path.Combine("instructions", "moved.md"), systemName, "bikeshare_moved_in_osm.geojson");

            //NOTE: Renames are handled in bulk via changeset, so no need to create individual tasks
            Serilog.Log.Information("Skipping 'renamed' challenge creation (handled via changeset)");
            //await CreateTaskForTypeAsync(projectId, "renamed", $"{systemName} -- Renamed stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
            //    Path.Combine("instructions", "renamed.md"), systemName, "bikeshare_renamed_in_osm.geojson");
        }

        /// <summary>
        /// Validates that a Maproulette project exists and is accessible (public method for testing)
        /// </summary>
        /// <param name="projectId">The project ID to validate</param>
        /// <returns>True if the project exists and is accessible, false otherwise</returns>
        public static async Task<bool> ValidateProjectAsync(int projectId)
        {
            return await ValidateProjectExistsAsync(projectId);
        }

        /// <summary>
        /// Validates that a Maproulette project exists and is accessible
        /// </summary>
        /// <param name="projectId">The project ID to validate</param>
        /// <returns>True if the project exists and is accessible, false otherwise</returns>
        private static async Task<bool> ValidateProjectExistsAsync(int projectId)
        {
            var client = HttpFactory.CreateClient();
            var apiKey = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                Serilog.Log.Error("MAPROULETTE_API_KEY environment variable is not set. Project validation requires API authentication.");
                throw new InvalidOperationException("MAPROULETTE_API_KEY environment variable is required for project validation and task creation.");
            }

            client.DefaultRequestHeaders.Add("apiKey", apiKey);

            try
            {
                // Try to fetch the project details from Maproulette API
                var response = await client.GetAsync($"https://maproulette.org/api/v2/project/{projectId}");

                if (response.IsSuccessStatusCode)
                {
                    var projectJson = await response.Content.ReadAsStringAsync();
                    var project = JsonSerializer.Deserialize<JsonElement>(projectJson);

                    if (project.TryGetProperty("name", out var nameProperty))
                    {
                        var projectName = nameProperty.GetString();
                        Serilog.Log.Information("Found Maproulette project {Name} (ID: {Id})", projectName, projectId);

                        // Check if project is enabled
                        if (project.TryGetProperty("enabled", out var enabledProperty))
                        {
                            var isEnabled = enabledProperty.GetBoolean();
                            if (!isEnabled)
                            {
                                Serilog.Log.Warning("Project {Name} (ID: {Id}) is disabled; tasks may not be visible", projectName, projectId);
                            }
                        }

                        return true;
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Serilog.Log.Error("Maproulette project {ProjectId} not found", projectId);
                    Serilog.Log.Information("Check: ID correctness, access permissions, project existence");
                    throw new ArgumentException($"Maproulette project {projectId} not found. Please verify the project ID and your access permissions.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Serilog.Log.Error("Unauthorized access to Maproulette project {ProjectId}", projectId);
                    Serilog.Log.Information("Ensure API key validity and project permissions");
                    throw new UnauthorizedAccessException($"Unauthorized access to Maproulette project {projectId}. Please check your API key and permissions.");
                }
                else
                {
                    Serilog.Log.Error("Failed to validate Maproulette project {ProjectId} - Status {Status}", projectId, response.StatusCode);
                    Serilog.Log.Debug("Validation response: {Body}", await response.Content.ReadAsStringAsync());
                    throw new InvalidOperationException($"Failed to validate Maproulette project {projectId}. HTTP Status: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Serilog.Log.Error(ex, "Network error validating Maproulette project {ProjectId}", projectId);
                Serilog.Log.Information("Check internet connection, site availability, retry later");
                throw new InvalidOperationException($"Network error while validating Maproulette project {projectId}: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                Serilog.Log.Error(ex, "Unexpected error validating Maproulette project {ProjectId}", projectId);
                throw new InvalidOperationException($"Unexpected error validating Maproulette project {projectId}: {ex.Message}", ex);
            }

            // This line should never be reached due to the exceptions above, but included for completeness
            return false;
        }

        private async static Task CreateTaskForTypeAsync(int projectId, string taskType, string challengeDescription, string instructionFilePath, string systemName, string fileName)
        {
            var client = HttpFactory.CreateClient();
            var apiKey = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("MAPROULETTE_API_KEY environment variable is not set. This is required for creating Maproulette tasks.");
            }

            client.DefaultRequestHeaders.Add("apiKey", apiKey);

            // Read instruction from markdown file using system-specific path
            if (!FileManager.SystemFileExists(systemName, instructionFilePath))
            {
                throw new FileNotFoundException($"Critical instruction file not found: {FileManager.GetSystemFilePath(systemName, instructionFilePath)}. Cannot create {taskType} challenge without instruction template.");
            }

            string instruction = await FileManager.ReadSystemTextFileAsync(systemName, instructionFilePath);
            if (string.IsNullOrWhiteSpace(instruction))
            {
                throw new InvalidOperationException($"Instruction file is empty: {FileManager.GetSystemFilePath(systemName, instructionFilePath)}. Cannot create {taskType} challenge without instruction content.");
            }

            // Check if file exists and has content
            if (!FileManager.SystemFileExists(systemName, fileName))
            {
                Serilog.Log.Information("No {Type} stations file {File} for system {System}; skipping challenge creation", taskType, fileName, systemName);
                return;
            }

            string fileContent = await FileManager.ReadSystemTextFileAsync(systemName, fileName);
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                Serilog.Log.Information("No {Type} stations found; skipping challenge creation", taskType);
                return;
            }

            // Convert each station to a Maproulette task
            var stations = fileContent
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimStart('\u001e'))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (stations.Count == 0)
            {
                Serilog.Log.Information("No valid {Type} stations parsed; skipping challenge creation", taskType);
                return;
            }

            var challengeName = $"{challengeDescription}";

            // Create challenge
            var challengeData = new
            {
                name = challengeName,
                description = challengeDescription,
                instruction = instruction,
                checkinComment = $"{taskType} stations changeset for {challengeName}",
                blurb = instruction,
                enabled = true,
                difficulty = taskType == "removed" ? 2 : (taskType == "added" ? 3 : 2),
                requiresLocal = false,
                parent = projectId
            };

            var challengeResponse = await client.PostAsync(
                "https://maproulette.org/api/v2/challenge",
                new StringContent(JsonSerializer.Serialize(challengeData), Encoding.UTF8, "application/json")
            );

            if (!challengeResponse.IsSuccessStatusCode)
            {
                var errorContent = await challengeResponse.Content.ReadAsStringAsync();
                Serilog.Log.Error("Failed to create {Type} challenge: {Error}", taskType, errorContent);

                // Provide helpful error messages based on common issues
                if (challengeResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Serilog.Log.Information("Authorization failure likely due to invalid API key or insufficient permissions");
                    throw new UnauthorizedAccessException($"Failed to create {taskType} challenge due to authorization issues. Check your API key and project permissions.");
                }
                else if (challengeResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    Serilog.Log.Information("Bad request: invalid data, inaccessible project, or duplicate name");
                    throw new InvalidOperationException($"Failed to create {taskType} challenge due to bad request. Check challenge parameters and project configuration.");
                }

                throw new InvalidOperationException($"Failed to create {taskType} challenge. HTTP Status: {challengeResponse.StatusCode}, Response: {errorContent}");
            }

            var challengeResult = JsonSerializer.Deserialize<JsonElement>(await challengeResponse.Content.ReadAsStringAsync());
            var challengeId = challengeResult.GetProperty("id").GetInt32();

            Serilog.Log.Information("Creating {Count} {Type} tasks", stations.Count, taskType);

            // Create tasks one by one
            int successCount = 0;
            int failureCount = 0;

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];

                var taskResponse = await client.PutAsync(
                    $"https://maproulette.org/api/v2/challenge/{challengeId}/addTasks",
                    new StringContent(station, Encoding.UTF8, "application/json")
                );

                if (taskResponse.IsSuccessStatusCode)
                {
                    successCount++;
                    Serilog.Log.Debug("Created {Type} task {Index}/{Total}", taskType, i + 1, stations.Count);
                }
                else
                {
                    failureCount++;
                    Serilog.Log.Warning("Failed to create {Type} task {Index}/{Total}: {Error}", taskType, i + 1, stations.Count, await taskResponse.Content.ReadAsStringAsync());
                }

                // Add a small delay to avoid overwhelming the API
                await Task.Delay(100);
            }

            //await ResetTaskInstructionsAsync(taskType, client, challengeName, challengeId);

            Serilog.Log.Information("{Type} challenge creation completed: {Name} (ID: {Id})", taskType.ToUpper(), challengeName, challengeId);
            Serilog.Log.Information("Task results - Success: {Success} Failed: {Fail} Total: {Total}", successCount, failureCount, stations.Count);

            // Provide link to the created challenge
            Serilog.Log.Information("View challenge: https://maproulette.org/admin/project/{ProjectId}/challenge/{ChallengeId}", projectId, challengeId);
        }

        private static async Task ResetTaskInstructionsAsync(string taskType, HttpClient client, string challengeName, int challengeId)
        {
            var resetResponse = await client.PutAsync(
                $"https://maproulette.org/api/v2/challenge/{challengeId}/resetTaskInstructions", null);

            if (resetResponse.IsSuccessStatusCode)
            {
                Serilog.Log.Information("Reset task instructions for {Type} challenge {Name} (ID: {Id})", taskType, challengeName, challengeId);
            }
            else
            {
                Serilog.Log.Warning("Failed to reset instructions for {Type} challenge {Name} (ID: {Id}): {Response}", taskType, challengeName, challengeId, await resetResponse.Content.ReadAsStringAsync());
            }
        }
    }
}
