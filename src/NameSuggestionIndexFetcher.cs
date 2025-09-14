using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using System.IO;

namespace prepareBikeParking
{
    /// <summary>
    /// Fetches brand data from the OpenStreetMap Name Suggestion Index and matches it with bike share systems
    /// </summary>
    public static class NameSuggestionIndexFetcher
    {
        private const string BicycleRentalBrandsUrl = "https://github.com/osmlab/name-suggestion-index/raw/main/data/brands/amenity/bicycle_rental.json";

        /// <summary>
        /// Fetches the bicycle rental brands data from the Name Suggestion Index
        /// </summary>
        /// <returns>Raw JSON string containing brand data</returns>
        public static async Task<string> FetchBicycleRentalBrandsAsync()
        {
            using var client = new HttpClient();
            Log.Information("Fetching bicycle rental brands from Name Suggestion Index: {Url}", BicycleRentalBrandsUrl);
            
            try
            {
                var json = await client.GetStringAsync(BicycleRentalBrandsUrl);
                Log.Debug("Successfully fetched {Length} characters of brand data", json.Length);
                return json;
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Failed to fetch bicycle rental brands from Name Suggestion Index");
                throw new InvalidOperationException($"Could not fetch brand data from Name Suggestion Index: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finds OSM tags for a bike share system based on its brand:wikidata value using pre-fetched JSON data
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="brandWikidata">Wikidata Q-identifier for the brand (e.g., "Q17018523")</param>
        /// <param name="brandsJson">Pre-fetched Name Suggestion Index JSON data</param>
        /// <returns>Dictionary of OSM tags if found, null if not found</returns>
        private static Dictionary<string, string>? FindOsmTagsForSystemFromJson(string systemName, string? brandWikidata, string brandsJson)
        {
            if (string.IsNullOrWhiteSpace(brandWikidata))
            {
                Log.Warning("No brand:wikidata value provided for system {System}", systemName);
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(brandsJson);
                var root = document.RootElement;

                if (!root.TryGetProperty("items", out var items))
                {
                    Log.Warning("No 'items' property found in Name Suggestion Index data");
                    return null;
                }

                // Search through all brand items
                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("tags", out var tags))
                        continue;

                    // Check if this brand has the matching wikidata ID
                    if (tags.TryGetProperty("brand:wikidata", out var wikidataProperty))
                    {
                        var wikidataValue = wikidataProperty.GetString();
                        if (string.Equals(wikidataValue, brandWikidata, StringComparison.OrdinalIgnoreCase))
                        {
                            var displayName = item.TryGetProperty("displayName", out var displayNameProp) 
                                ? displayNameProp.GetString() ?? "Unknown"
                                : "Unknown";
                            
                            Log.Information("Found matching brand for system {System} with wikidata {Wikidata}: {DisplayName}", 
                                systemName, brandWikidata, displayName);

                            // Convert JsonElement tags to Dictionary
                            var osmTags = new Dictionary<string, string>();
                            foreach (var tag in tags.EnumerateObject())
                            {
                                var value = tag.Value.GetString();
                                if (!string.IsNullOrEmpty(value))
                                {
                                    osmTags[tag.Name] = value;
                                }
                            }

                            return osmTags;
                        }
                    }
                }

                Log.Warning("No matching brand found for system {System} with wikidata {Wikidata}", systemName, brandWikidata);
                return null;
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to parse Name Suggestion Index JSON data");
                throw new InvalidOperationException($"Invalid JSON format in Name Suggestion Index data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finds OSM tags for a bike share system based on its brand:wikidata value
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="brandWikidata">Wikidata Q-identifier for the brand (e.g., "Q17018523")</param>
        /// <returns>Dictionary of OSM tags if found, null if not found</returns>
        public static async Task<Dictionary<string, string>?> FindOsmTagsForSystemAsync(string systemName, string? brandWikidata)
        {
            var brandsJson = await FetchBicycleRentalBrandsAsync();
            return FindOsmTagsForSystemFromJson(systemName, brandWikidata, brandsJson);
        }

        /// <summary>
        /// Saves OSM tags for a bike share system to an OSM tag file in the system's directory
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="osmTags">Dictionary of OSM tags to save</param>
        /// <param name="fileName">Name of the file to save (default: "brand_tags.osm")</param>
        public static async Task SaveOsmTagsForSystemAsync(string systemName, Dictionary<string, string> osmTags, string fileName = "brand_tags.osm")
        {
            if (osmTags == null || osmTags.Count == 0)
            {
                Log.Warning("No OSM tags to save for system {System}", systemName);
                return;
            }

            try
            {
                var osmTagsContent = GenerateOsmTagsContent(osmTags, systemName);
                
                await FileManager.WriteSystemTextFileAsync(systemName, fileName, osmTagsContent);

                var fullPath = FileManager.GetSystemFullPath(systemName, fileName);
                Log.Information("Saved {Count} OSM brand tags for system {System} to {FullPath}", 
                    osmTags.Count, systemName, fullPath);

                // Log the tags for visibility
                foreach (var tag in osmTags)
                {
                    Log.Debug("OSM tag for {System}: {Key}={Value}", systemName, tag.Key, tag.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save OSM tags for system {System}", systemName);
                throw new InvalidOperationException($"Could not save OSM tags for system {systemName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates OSM tag content in key=value format from a dictionary of tags
        /// </summary>
        /// <param name="osmTags">Dictionary of OSM tags</param>
        /// <param name="systemName">Name of the bike share system (for comments)</param>
        /// <returns>OSM tags string in key=value format</returns>
        private static string GenerateOsmTagsContent(Dictionary<string, string> osmTags, string systemName)
        {
            var content = new System.Text.StringBuilder();
            
            foreach (var tag in osmTags.OrderBy(t => t.Key))
            {
                content.AppendLine($"{tag.Key}={tag.Value}");
            }
            
            return content.ToString();
        }

        /// <summary>
        /// Fetches and saves OSM brand tags for a specific bike share system
        /// </summary>
        /// <param name="system">Bike share system configuration</param>
        /// <param name="fileName">Name of the file to save (default: "brand_tags.osm")</param>
        /// <returns>True if tags were found and saved, false otherwise</returns>
        public static async Task<bool> FetchAndSaveOsmTagsForSystemAsync(BikeShareSystem system, string fileName = "brand_tags.osm")
        {
            Log.Information("Fetching OSM brand tags for system {System} ({City})", system.Name, system.City);

            var osmTags = await FindOsmTagsForSystemAsync(system.Name, system.BrandWikidata);
            
            if (osmTags == null || osmTags.Count == 0)
            {
                Log.Information("No OSM brand tags found for system {System}", system.Name);
                return false;
            }

            await SaveOsmTagsForSystemAsync(system.Name, osmTags, fileName);
            return true;
        }

        /// <summary>
        /// Fetches and saves OSM brand tags for all configured bike share systems
        /// </summary>
        /// <param name="fileName">Name of the file to save for each system (default: "brand_tags.osm")</param>
        /// <returns>Number of systems for which tags were successfully saved</returns>
        public static async Task<int> FetchAndSaveOsmTagsForAllSystemsAsync(string fileName = "brand_tags.osm")
        {
            Log.Information("Fetching OSM brand tags for all configured bike share systems");

            var systems = await BikeShareSystemLoader.LoadAllSystemsAsync();
            
            // Fetch NSI data once for all systems
            string brandsJson;
            try
            {
                brandsJson = await FetchBicycleRentalBrandsAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch Name Suggestion Index data for all systems");
                return 0;
            }

            var successCount = 0;

            foreach (var system in systems)
            {
                try
                {
                    Log.Information("Processing OSM brand tags for system {System} ({City})", system.Name, system.City);

                    var osmTags = FindOsmTagsForSystemFromJson(system.Name, system.BrandWikidata, brandsJson);
                    
                    if (osmTags == null || osmTags.Count == 0)
                    {
                        Log.Information("No OSM brand tags found for system {System}", system.Name);
                        continue;
                    }

                    await SaveOsmTagsForSystemAsync(system.Name, osmTags, fileName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to process OSM brand tags for system {System}", system.Name);
                    // Continue with other systems
                }
            }

            Log.Information("Successfully saved OSM brand tags for {Success}/{Total} systems", successCount, systems.Count);
            return successCount;
        }
    }
}