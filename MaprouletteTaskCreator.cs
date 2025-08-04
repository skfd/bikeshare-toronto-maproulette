using System.Text;
using System.Text.Json;

namespace prepareBikeParking
{
    public static class MaprouletteTaskCreator
    {
        public async static Task CreateTasksAsync(int projectId, DateTime lastSyncDate)
        {
            Console.WriteLine("Creating Maproulette tasks...");

             //Create challenges for each type of change
            await CreateTaskForTypeAsync(projectId, "removed", $"Bikeshare Toronto -- Removed stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                "../../../instructions/removed.md", "../../../bikeshare_removed.geojson");

            await CreateTaskForTypeAsync(projectId, "added", $"Bikeshare Toronto -- Added stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                "../../../instructions/added.md", "../../../bikeshare_added.geojson");

            await CreateTaskForTypeAsync(projectId, "moved", $"Bikeshare Toronto -- Moved stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                "../../../instructions/moved.md", "../../../bikeshare_moved.geojson");

            await CreateTaskForTypeAsync(projectId, "renamed", $"Bikeshare Toronto -- Renamed stations at {DateTime.Now:yyyy-MM-dd} since {lastSyncDate:yyyy-MM-dd}",
                "../../../instructions/renamed.md", "../../../bikeshare_renamed.geojson");
        }


        private async static Task CreateTaskForTypeAsync(int projectId, string taskType, string challengeDescription, string instructionFilePath, string filePath)
        {
            var client = new HttpClient();
            var apiKey = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("MAPROULETTE_API_KEY environment variable is not set.");
                return;
            }

            client.DefaultRequestHeaders.Add("apiKey", apiKey);

            // Read instruction from markdown file
            if (!File.Exists(instructionFilePath))
            {
                Console.WriteLine($"Instruction file not found at {instructionFilePath}. Skipping {taskType} challenge creation.");
                return;
            }

            string instruction = File.ReadAllText(instructionFilePath);
            if (string.IsNullOrWhiteSpace(instruction))
            {
                Console.WriteLine($"Instruction file is empty at {instructionFilePath}. Skipping {taskType} challenge creation.");
                return;
            }

            // Check if file exists and has content
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"No {taskType} stations file found at {filePath}. Skipping {taskType} challenge creation.");
                return;
            }

            string fileContent = File.ReadAllText(filePath);
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
                enabled = false,
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
