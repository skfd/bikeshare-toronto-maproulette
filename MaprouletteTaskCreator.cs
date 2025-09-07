using System.Text;
using System.Text.Json;

namespace prepareBikeParking
{
    public static class MaprouletteTaskCreator
    {
        public async static Task CreateTasksAsync(int projectId, DateTime lastSyncDate, string systemName = "Toronto", bool isNewSystem = false)
        {
            Console.WriteLine("Creating Maproulette tasks...");

            // First, validate that the project exists and is accessible
            if (!await ValidateProjectExistsAsync(projectId))
            {
                throw new InvalidOperationException($"Maproulette project validation failed for project ID {projectId}. Cannot proceed with task creation.");
            }

            Console.WriteLine($"✅ Maproulette project {projectId} validated successfully.");

            if (isNewSystem)
            {
                Console.WriteLine("🆕 Detected new system setup - skipping 'removed' task creation to avoid deleting existing OSM data.");
                Console.WriteLine("   Only 'added' and 'moved' tasks will be created for stations missing from or different in OSM.");
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
                Console.WriteLine("⏭️  Skipping 'removed' challenge creation for new system to preserve existing OSM data.");
            }

            await CreateTaskForTypeAsync(projectId, "added", $"{systemName} -- Added stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                Path.Combine("instructions", "added.md"), systemName, "bikeshare_missing_in_osm.geojson");

            await CreateTaskForTypeAsync(projectId, "moved", $"{systemName} -- Moved stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                Path.Combine("instructions", "moved.md"), systemName, "bikeshare_moved_in_osm.geojson");

            //NOTE: Renames are handled in bulk via changeset, so no need to create individual tasks
            Console.WriteLine("Skipping 'renamed' challenge creation as renames are handled via changeset.");
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
            var client = new HttpClient();
            var apiKey = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ MAPROULETTE_API_KEY environment variable is not set.");
                Console.WriteLine("   Project validation requires API authentication.");
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
                        Console.WriteLine($"✅ Found Maproulette project: '{projectName}' (ID: {projectId})");
                        
                        // Check if project is enabled
                        if (project.TryGetProperty("enabled", out var enabledProperty))
                        {
                            var isEnabled = enabledProperty.GetBoolean();
                            if (!isEnabled)
                            {
                                Console.WriteLine($"⚠️  Warning: Project '{projectName}' is disabled.");
                                Console.WriteLine("   Tasks can still be created but may not be visible to mappers.");
                            }
                        }

                        return true;
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"❌ Maproulette project with ID {projectId} not found.");
                    Console.WriteLine("   Possible solutions:");
                    Console.WriteLine($"   1. Verify the project ID is correct: https://maproulette.org/admin/project/{projectId}");
                    Console.WriteLine("   2. Ensure you have access to the project");
                    Console.WriteLine("   3. Check that the project hasn't been deleted");
                    throw new ArgumentException($"Maproulette project {projectId} not found. Please verify the project ID and your access permissions.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine($"❌ Unauthorized access to Maproulette project {projectId}.");
                    Console.WriteLine("   Possible solutions:");
                    Console.WriteLine("   1. Verify your MAPROULETTE_API_KEY is correct");
                    Console.WriteLine("   2. Check that you have permission to access this project");
                    Console.WriteLine("   3. Ensure your API key hasn't expired");
                    throw new UnauthorizedAccessException($"Unauthorized access to Maproulette project {projectId}. Please check your API key and permissions.");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to validate Maproulette project {projectId}.");
                    Console.WriteLine($"   HTTP Status: {response.StatusCode}");
                    Console.WriteLine($"   Response: {await response.Content.ReadAsStringAsync()}");
                    throw new InvalidOperationException($"Failed to validate Maproulette project {projectId}. HTTP Status: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ Network error while validating Maproulette project {projectId}: {ex.Message}");
                Console.WriteLine("   Possible solutions:");
                Console.WriteLine("   1. Check your internet connection");
                Console.WriteLine("   2. Verify Maproulette.org is accessible");
                Console.WriteLine("   3. Try again later if the service is temporarily unavailable");
                throw new InvalidOperationException($"Network error while validating Maproulette project {projectId}: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException && ex is not UnauthorizedAccessException)
            {
                Console.WriteLine($"❌ Unexpected error validating Maproulette project {projectId}: {ex.Message}");
                throw new InvalidOperationException($"Unexpected error validating Maproulette project {projectId}: {ex.Message}", ex);
            }

            // This line should never be reached due to the exceptions above, but included for completeness
            return false;
        }

        private async static Task CreateTaskForTypeAsync(int projectId, string taskType, string challengeDescription, string instructionFilePath, string systemName, string fileName)
        {
            var client = new HttpClient();
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
                Console.WriteLine($"No {taskType} stations file found at {fileName} for system {systemName}. Skipping {taskType} challenge creation.");
                return;
            }

            string fileContent = await FileManager.ReadSystemTextFileAsync(systemName, fileName);
            if (string.IsNullOrWhiteSpace(fileContent))
            {
                Console.WriteLine($"No {taskType} stations found. Skipping {taskType} challenge creation.");
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
                Console.WriteLine($"No valid {taskType} stations found. Skipping {taskType} challenge creation.");
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
                Console.WriteLine($"Failed to create {taskType} challenge: {errorContent}");
                
                // Provide helpful error messages based on common issues
                if (challengeResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("   This might be due to:");
                    Console.WriteLine("   1. Invalid or expired MAPROULETTE_API_KEY");
                    Console.WriteLine("   2. Insufficient permissions for the project");
                    throw new UnauthorizedAccessException($"Failed to create {taskType} challenge due to authorization issues. Check your API key and project permissions.");
                }
                else if (challengeResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    Console.WriteLine("   This might be due to:");
                    Console.WriteLine("   1. Invalid challenge data or parameters");
                    Console.WriteLine("   2. Project ID doesn't exist or isn't accessible");
                    Console.WriteLine("   3. Duplicate challenge name");
                    throw new InvalidOperationException($"Failed to create {taskType} challenge due to bad request. Check challenge parameters and project configuration.");
                }
                
                throw new InvalidOperationException($"Failed to create {taskType} challenge. HTTP Status: {challengeResponse.StatusCode}, Response: {errorContent}");
            }

            var challengeResult = JsonSerializer.Deserialize<JsonElement>(await challengeResponse.Content.ReadAsStringAsync());
            var challengeId = challengeResult.GetProperty("id").GetInt32();

            Console.WriteLine($"Creating {stations.Count} {taskType} tasks...");

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
                    Console.WriteLine($"Successfully created {taskType} task {i + 1}/{stations.Count}");
                }
                else
                {
                    failureCount++;
                    Console.WriteLine($"Failed to create {taskType} task {i + 1}/{stations.Count}: {await taskResponse.Content.ReadAsStringAsync()}");
                }

                // Add a small delay to avoid overwhelming the API
                await Task.Delay(100);
            }

            //await ResetTaskInstructionsAsync(taskType, client, challengeName, challengeId);

            Console.WriteLine($"{taskType.ToUpper()} challenge creation completed: {challengeName} (ID: {challengeId})");
            Console.WriteLine($"Tasks created successfully: {successCount}, Failed: {failureCount}, Total: {stations.Count}");
            
            // Provide link to the created challenge
            Console.WriteLine($"View challenge: https://maproulette.org/admin/project/{projectId}/challenge/{challengeId}");
        }

        private static async Task ResetTaskInstructionsAsync(string taskType, HttpClient client, string challengeName, int challengeId)
        {
            var resetResponse = await client.PutAsync(
                $"https://maproulette.org/api/v2/challenge/{challengeId}/resetTaskInstructions", null);

            if (resetResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Successfully reset task instructions for {taskType} challenge {challengeName} (ID: {challengeId})");
            }
            else
            {
                Console.WriteLine($"Failed to reset task instructions for {taskType} challenge {challengeName} (ID: {challengeId}): {await resetResponse.Content.ReadAsStringAsync()}");
            }
        }
    }
}
