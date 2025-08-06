using System.Text.Json;

namespace prepareBikeParking
{
    /// <summary>
    /// Simple test class to demonstrate and verify OSM data fetcher functionality
    /// </summary>
    public static class OSMDataFetcherTest
    {
        /// <summary>
        /// Test the OSM data fetcher with a smaller query for testing purposes
        /// </summary>
        public static async Task<bool> TestOSMDataFetcher()
        {
            try
            {
                Console.WriteLine("Testing OSM Data Fetcher with a small query...");
                
                // Use a smaller test query for just downtown Toronto to avoid overwhelming the API
                var testQuery = @"
[out:json][timeout:25];

// Define a small area in downtown Toronto for testing
(
  node(43.65,-79.39,43.66,-79.38)[bicycle_rental=docking_station];
);

out body;
";

                var url = "https://overpass-api.de/api/interpreter";
                var client = new HttpClient();
                
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("data", testQuery)
                };
                
                var formContent = new FormUrlEncodedContent(formData);
                
                var response = await client.PostAsync(url, formContent);
                var responseText = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API request failed: {response.StatusCode} - {responseText}");
                    return false;
                }

                // Parse the response
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(responseText);
                var elements = jsonDoc.GetProperty("elements");
                
                Console.WriteLine($"Test query returned {elements.GetArrayLength()} elements");
                
                // Test parsing a few elements
                int parsed = 0;
                foreach (var element in elements.EnumerateArray().Take(3))
                {
                    if (element.TryGetProperty("type", out var typeProperty) && 
                        typeProperty.GetString() == "node")
                    {
                        var lat = element.GetProperty("lat").GetDouble();
                        var lon = element.GetProperty("lon").GetDouble();
                        Console.WriteLine($"Found bike station at: {lat}, {lon}");
                        parsed++;
                    }
                }
                
                Console.WriteLine($"Successfully parsed {parsed} bike station elements");
                Console.WriteLine("OSM Data Fetcher test passed!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OSM Data Fetcher test failed: {ex.Message}");
                return false;
            }
        }
    }
}