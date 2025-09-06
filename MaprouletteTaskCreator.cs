using System.Text;
using System.Text.Json;

namespace prepareBikeParking
{
    public static class MaprouletteTaskCreator
    {
        public async static Task CreateTasksAsync(int projectId, DateTime lastSyncDate, string systemName = "Toronto")
        {
            Console.WriteLine("Creating Maproulette tasks...");

             //Create challenges for each type of change
            await CreateTaskForTypeAsync(projectId, "removed", $"{systemName} -- Removed stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                Path.Combine("instructions", "removed.md"), systemName, "bikeshare_extra_in_osm.geojson");

            await CreateTaskForTypeAsync(projectId, "added", $"{systemName} -- Added stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                Path.Combine("instructions", "added.md"), systemName, "bikeshare_missing_in_osm.geojson");

            await CreateTaskForTypeAsync(projectId, "moved", $"{systemName} -- Moved stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                Path.Combine("instructions", "moved.md"), systemName, "bikeshare_moved.geojson");

            //NOTE: Renames are handled in bulk via changeset, so no need to create individual tasks
            Console.WriteLine("Skipping 'renamed' challenge creation as renames are handled via changeset.");
            //await CreateTaskForTypeAsync(projectId, "renamed", $"{systemName} -- Renamed stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
            //    Path.Combine("instructions", "renamed.md"), systemName, "bikeshare_renamed_in_osm.geojson");
        }


        private async static Task CreateTaskForTypeAsync(int projectId, string taskType, string challengeDescription, string instructionFilePath, string systemName, string fileName)
        {
            var client = new HttpClient();
            var apiKey = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("MAPROULETTE_API_KEY environment variable is not set.");
                return;
            }

            client.DefaultRequestHeaders.Add("apiKey", apiKey);

            // Read instruction from markdown file using system-specific path
            if (!FileManager.SystemFileExists(systemName, instructionFilePath))
            {
                Console.WriteLine($"Instruction file not found at {FileManager.GetSystemFilePath(systemName, instructionFilePath)}. Skipping {taskType} challenge creation.");
                return;
            }

            string instruction = await FileManager.ReadSystemTextFileAsync(systemName, instructionFilePath);
            if (string.IsNullOrWhiteSpace(instruction))
            {
                Console.WriteLine($"Instruction file is empty at {FileManager.GetSystemFilePath(systemName, instructionFilePath)}. Skipping {taskType} challenge creation.");
                return;
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
                Console.WriteLine($"Failed to create {taskType} challenge: {await challengeResponse.Content.ReadAsStringAsync()}");
                return;
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
