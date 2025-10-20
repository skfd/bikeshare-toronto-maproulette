using System.Text.Json;
using Serilog;

namespace prepareBikeParking
{
    public interface IOverpassHttpClientFactory
    {
        HttpClient CreateClient();
    }

    public class DefaultOverpassHttpClientFactory : IOverpassHttpClientFactory
    {
        public HttpClient CreateClient() => new HttpClient();
    }

    public class OSMDataFetcher
    {
        private readonly IOverpassHttpClientFactory _clientFactory;
        public OSMDataFetcher() : this(new DefaultOverpassHttpClientFactory()) {}
        public OSMDataFetcher(IOverpassHttpClientFactory clientFactory) { _clientFactory = clientFactory; }
        /// <summary>
        /// Fetches bikeshare station data from OpenStreetMap using system-specific Overpass query file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
    public async Task<List<GeoPoint>> FetchFromOverpassApiAsync(string systemName)
        {
            // Try to load system-specific overpass query first
            string overpassQuery;
            try
            {
                overpassQuery = await FileManager.ReadSystemTextFileAsync(systemName, "stations.overpass");
                Log.Information("Using system-specific Overpass query {File}", $"data_results/{systemName}/stations.overpass");
            }
            catch (FileNotFoundException)
            {
                // Fallback to default query generation for backward compatibility
                Log.Warning("System-specific stations.overpass file not found for {System}. Using fallback query generation.", systemName);
                Log.Information("Consider creating data_results/{System}/stations.overpass for customization.", systemName);
                overpassQuery = GenerateDefaultOverpassQuery(systemName);
            }

            var url = "https://overpass-api.de/api/interpreter";
            var client = _clientFactory.CreateClient();

            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("data", overpassQuery)
            };

            var formContent = new FormUrlEncodedContent(formData);

            var response = await client.PostAsync(url, formContent);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Overpass API request failed: {response.StatusCode} - {responseText}");
            }

            var osmData = await ParseOverpassResponseAsync(responseText, systemName);

            // Save the OSM data to bikeshare_osm.geojson file
            await SaveOsmDataAsync(osmData, systemName);

            return osmData;
        }

        /// <summary>
        /// Saves OSM data to bikeshare_osm.geojson file next to other geojson files
        /// </summary>
        /// <param name="osmData">List of GeoPoint objects from OSM</param>
        /// <param name="systemName">Name of the bike share system</param>
    private static async Task SaveOsmDataAsync(List<GeoPoint> osmData, string systemName)
        {
            try
            {
                await FileManager.WriteSystemGeoJsonFileAsync(
                    systemName,
                    "bikeshare_osm.geojson",
                    osmData,
                    point => GeoJsonGenerator.GenerateGeojsonLine(point, systemName)
                );

                Log.Information("Saved {Count} OSM stations to {Path}", osmData.Count, $"data_results/{systemName}/bikeshare_osm.geojson");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not save OSM data to file for {System}", systemName);
                // Don't throw - this is a nice-to-have feature, shouldn't break the main flow
            }
        }

        /// <summary>
        /// Generates a default Overpass query for systems without a stations.overpass file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <returns>Default Overpass query string</returns>
    private static string GenerateDefaultOverpassQuery(string systemName)
        {
            // Map system names to cities for fallback
            var cityName = systemName switch
            {
                "Bike Share Toronto" => "Toronto",
                "Bixi" => "Montreal", // Note: This won't work perfectly for Bixi's complex area query
                _ => systemName // Fallback to system name as city name
            };

            return $@"
                [out:json];

                area[name=""{cityName}""]->.city;
                (
                  node(area.city)[bicycle_rental=docking_station];
                  way(area.city)[bicycle_rental=docking_station];
                  relation(area.city)[bicycle_rental=docking_station];
                );

                out meta;
                ";
        }

        /// <summary>
        /// Creates a default stations.overpass file for a system if it doesn't exist
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="cityName">Name of the city for the query</param>
    public static async Task EnsureStationsOverpassFileAsync(string systemName, string cityName)
        {
            if (!FileManager.SystemFileExists(systemName, "stations.overpass"))
            {
                var defaultQuery = $@"[out:json];

area[name=""{cityName}""]->.city;
(
    node(area.city)[bicycle_rental=docking_station];
    way(area.city)[bicycle_rental=docking_station];
    relation(area.city)[bicycle_rental=docking_station];
);

out meta;
";

                await FileManager.WriteSystemTextFileAsync(systemName, "stations.overpass", defaultQuery);
                Log.Information("Created default stations.overpass file for {System}", systemName);
                Log.Information("Edit data_results/{System}/stations.overpass to customize the Overpass query", systemName);
            }
        }

        /// <summary>
        /// Parses the Overpass API JSON response into GeoPoint objects
        /// </summary>
        /// <param name="jsonResponse">JSON response from Overpass API</param>
        /// <param name="systemName">Name of the bike share system (for validation reporting)</param>
    private async Task<List<GeoPoint>> ParseOverpassResponseAsync(string jsonResponse, string systemName)
        {
            var geoPoints = new List<GeoPoint>();

            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
            var elements = jsonDoc.GetProperty("elements");

            // First pass: collect all missing node IDs for batch retrieval
            var missingNodeIds = new HashSet<long>();
            var wayElements = new List<(JsonElement element, JsonElement tags, long firstNodeId)>();

            foreach (var element in elements.EnumerateArray())
            {
                element.TryGetProperty("type", out var typeProperty);
                var type = typeProperty.GetString();

                // Check if tags property exists before using it
                if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object)
                {
                    continue; // Skip elements without tags or with invalid tags
                }

                if (type == "node")
                {
                    // Check if it's a bikeshare station
                    if (tags.TryGetProperty("bicycle_rental", out var rentalProp) &&
                        rentalProp.GetString() == "docking_station")
                    {
                        var geoPoint = new GeoPoint
                        {
                            id = string.Empty,
                            name = string.Empty,
                            lat = "0",
                            lon = "0",
                            osmId = element.GetProperty("id").GetInt64().ToString(),
                            osmType = type,
                            osmVersion = element.GetProperty("version").GetInt32(),
                            osmXmlElement = element
                        };

                        // Get coordinates
                        if (element.TryGetProperty("lat", out var latProp))
                            geoPoint.lat = latProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

                        if (element.TryGetProperty("lon", out var lonProp))
                            geoPoint.lon = lonProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

                        // Get station reference ID (used by BikeShare Toronto)
                        if (tags.TryGetProperty("ref", out var refProp))
                        {
                            geoPoint.id = refProp.GetString() ?? "";
                        }
                        else
                        {
                            // If no ref, use OSM node ID as fallback
                            if (element.TryGetProperty("id", out var idProp))
                                geoPoint.id = "osm_" + idProp.GetInt64().ToString();
                        }

                        // Get station name
                        if (tags.TryGetProperty("name", out var nameProp))
                        {
                            geoPoint.name = nameProp.GetString() ?? "";
                        }
                        else
                        {
                            geoPoint.name = "Unnamed Station";
                        }

                        // Get capacity if available
                        if (tags.TryGetProperty("capacity", out var capacityProp))
                        {
                            if (int.TryParse(capacityProp.GetString(), out var capacity))
                                geoPoint.capacity = capacity;
                        }

                        // Only add if we have valid coordinates and ID
                        if (!string.IsNullOrEmpty(geoPoint.lat) &&
                            !string.IsNullOrEmpty(geoPoint.lon) &&
                            !string.IsNullOrEmpty(geoPoint.id) &&
                            !(geoPoint.lat == "0" && geoPoint.lon == "0"))
                        {
                            geoPoints.Add(geoPoint);
                        }
                    }
                }
                else if (type == "way")
                {
                    // Check if it's a bikeshare station
                    if (tags.TryGetProperty("bicycle_rental", out var rentalProp) &&
                        rentalProp.GetString() == "docking_station")
                    {
                        // Get the nodes array from the way
                        if (element.TryGetProperty("nodes", out var nodesProperty) &&
                            nodesProperty.ValueKind == JsonValueKind.Array)
                        {
                            var nodesArray = nodesProperty.EnumerateArray().ToArray();
                            if (nodesArray.Length > 0)
                            {
                                // Get the first node ID
                                var firstNodeId = nodesArray[0].GetInt64();

                                // Check if node exists in current elements
                                var nodeElement = FindNodeInElements(elements, firstNodeId);
                                if (nodeElement.HasValue)
                                {
                                    // Process immediately if node is found
                                    ProcessWayElement(element, tags, nodeElement.Value, geoPoints);
                                }
                                else
                                {
                                    // Store for batch retrieval
                                    missingNodeIds.Add(firstNodeId);
                                    wayElements.Add((element, tags, firstNodeId));
                                }
                            }
                        }
                    }
                }
            }

            // Batch fetch missing nodes if any
            Dictionary<long, JsonElement> fetchedNodes = new();
            if (missingNodeIds.Count > 0)
            {
                Log.Debug("Fetching {Count} missing nodes in batch for way centroids", missingNodeIds.Count);
                // Use this instance to call FetchNodesBatchAsync to reuse the client factory
                fetchedNodes = await FetchNodesBatchAsync(missingNodeIds.ToList());
            }

            // Second pass: process way elements with fetched nodes
            foreach (var (element, tags, firstNodeId) in wayElements)
            {
                if (fetchedNodes.TryGetValue(firstNodeId, out var nodeElement))
                {
                    ProcessWayElement(element, tags, nodeElement, geoPoints);
                }
                else
                {
                    Log.Warning("Could not fetch coordinates for node {NodeId}", firstNodeId);
                }
            }

            // Validate for duplicate ref values
            await ValidateAndReportDuplicates(geoPoints, systemName);

            return geoPoints;
        }

        /// <summary>
        /// Validates OSM data for duplicate ref values and generates a report if found
        /// </summary>
        private static async Task ValidateAndReportDuplicates(List<GeoPoint> geoPoints, string systemName)
        {
            // Group by ref ID, excluding auto-generated IDs (those starting with "osm_")
            var duplicates = geoPoints
                .Where(p => !string.IsNullOrEmpty(p.id) && !p.id.StartsWith("osm_"))
                .GroupBy(p => p.id)
                .Where(g => g.Count() > 1)
                .ToList();

            if (duplicates.Any())
            {
                var duplicateStations = duplicates.SelectMany(g => g).ToList();
                var totalDuplicateRefs = duplicates.Count;
                var totalAffectedStations = duplicateStations.Count;

                // Log warning to console
                Log.Warning("Found {DuplicateRefs} duplicate ref values in OSM data for {System}. " +
                           "Total affected stations: {AffectedStations}. " +
                           "See data_results/{System}/bikeshare_osm_duplicates.geojson for details",
                           totalDuplicateRefs, systemName, totalAffectedStations, systemName);

                // Log each duplicate ref with OSM element details
                foreach (var dup in duplicates)
                {
                    var osmDetails = string.Join(", ", dup.Select(d => $"{d.osmType}/{d.osmId}"));
                    Log.Warning("  Duplicate ref '{Ref}' found in {Count} OSM elements: {OsmIds}",
                        dup.Key, dup.Count(), osmDetails);
                }

                // Create a dictionary to lookup duplicate counts by ref
                var duplicateCounts = duplicates.ToDictionary(g => g.Key, g => g.Count());

                // Generate GeoJSON file with duplicate stations for editor review
                try
                {
                    await FileManager.WriteSystemGeoJsonFileAsync(
                        systemName,
                        "bikeshare_osm_duplicates.geojson",
                        duplicateStations,
                        point => GeoJsonGenerator.GenerateGeojsonLineWithError(
                            point,
                            systemName,
                            $"Duplicate ref '{point.id}' appears {duplicateCounts[point.id]} times in OSM (this is OSM {point.osmType}/{point.osmId})"
                        )
                    );

                    Log.Information("Duplicate validation report saved to data_results/{System}/bikeshare_osm_duplicates.geojson",
                        systemName);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not save duplicate validation report for {System}", systemName);
                }
            }
            else
            {
                Log.Debug("No duplicate ref values found in OSM data for {System}", systemName);
            }
        }

        /// <summary>
        /// Generates an enhanced duplicate report that includes GBFS data for comparison
        /// </summary>
        /// <param name="osmPoints">OSM data points that may contain duplicates</param>
        /// <param name="gbfsPoints">GBFS/API data points for reference</param>
        /// <param name="systemName">Name of the bike share system</param>
        public static async Task GenerateEnhancedDuplicateReportAsync(List<GeoPoint> osmPoints, List<GeoPoint> gbfsPoints, string systemName)
        {
            // Find duplicate ref values in OSM data
            var duplicates = osmPoints
                .Where(p => !string.IsNullOrEmpty(p.id) && !p.id.StartsWith("osm_"))
                .GroupBy(p => p.id)
                .Where(g => g.Count() > 1)
                .ToList();

            if (!duplicates.Any())
            {
                Log.Debug("No duplicate ref values found - enhanced report not needed");
                return;
            }

            var enhancedStations = new List<GeoPoint>();
            var duplicateCounts = duplicates.ToDictionary(g => g.Key, g => g.Count());

            foreach (var duplicateGroup in duplicates)
            {
                var refValue = duplicateGroup.Key;
                var count = duplicateGroup.Count();

                // Add all OSM stations with this duplicate ref
                foreach (var osmStation in duplicateGroup)
                {
                    enhancedStations.Add(osmStation);
                }

                // Find matching GBFS station (if any) with this ref
                var gbfsStation = gbfsPoints.FirstOrDefault(g => g.id == refValue);
                if (gbfsStation != null)
                {
                    // Add GBFS station for comparison with special marker
                    var gbfsForComparison = new GeoPoint
                    {
                        id = gbfsStation.id,
                        name = gbfsStation.name,
                        lat = gbfsStation.lat,
                        lon = gbfsStation.lon,
                        capacity = gbfsStation.capacity,
                        osmType = "GBFS",  // Mark as GBFS data
                        osmId = "official", // Mark as official source
                        osmVersion = 0,
                        osmXmlElement = default(JsonElement)
                    };
                    enhancedStations.Add(gbfsForComparison);
                }
            }

            // Generate enhanced GeoJSON file
            try
            {
                await FileManager.WriteSystemGeoJsonFileAsync(
                    systemName,
                    "bikeshare_osm_duplicates.geojson",
                    enhancedStations,
                    point => {
                        var refValue = point.id;
                        var count = duplicateCounts.ContainsKey(refValue) ? duplicateCounts[refValue] : 0;

                        string errorMsg;
                        if (point.osmType == "GBFS")
                        {
                            errorMsg = $"OFFICIAL GBFS DATA for ref '{refValue}' - Compare with OSM duplicates above to verify which is correct";
                        }
                        else
                        {
                            errorMsg = $"Duplicate ref '{refValue}' appears {count} times in OSM (this is OSM {point.osmType}/{point.osmId})";
                        }

                        return GeoJsonGenerator.GenerateGeojsonLineWithError(point, systemName, errorMsg);
                    }
                );

                Log.Information("Enhanced duplicate report with GBFS comparison saved to data_results/{System}/bikeshare_osm_duplicates.geojson", systemName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate enhanced duplicate report for {System}", systemName);
            }
        }

        /// <summary>
        /// Processes a way element and adds it to the geoPoints list
        /// </summary>
    private static void ProcessWayElement(JsonElement element, JsonElement tags, JsonElement nodeElement, List<GeoPoint> geoPoints)
        {
            var geoPoint = new GeoPoint
            {
                id = string.Empty,
                name = string.Empty,
                lat = "0",
                lon = "0",
                osmId = element.GetProperty("id").GetInt64().ToString(),
                osmType = "way",
                osmVersion = element.GetProperty("version").GetInt32(),
                osmXmlElement = element
            };

            // Get coordinates from the referenced node
            if (nodeElement.TryGetProperty("lat", out var latProp))
                geoPoint.lat = latProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (nodeElement.TryGetProperty("lon", out var lonProp))
                geoPoint.lon = lonProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Get station reference ID from way tags
            if (tags.TryGetProperty("ref", out var refProp))
            {
                geoPoint.id = refProp.GetString() ?? "";
            }
            else
            {
                // If no ref, use OSM way ID as fallback
                if (element.TryGetProperty("id", out var idProp))
                    geoPoint.id = "osm_way_" + idProp.GetInt64().ToString();
            }

            // Get station name from way tags
            if (tags.TryGetProperty("name", out var nameProp))
            {
                geoPoint.name = nameProp.GetString() ?? "";
            }
            else
            {
                geoPoint.name = "Unnamed Station";
            }

            // Get capacity from way tags if available
            if (tags.TryGetProperty("capacity", out var capacityProp))
            {
                if (int.TryParse(capacityProp.GetString(), out var capacity))
                    geoPoint.capacity = capacity;
            }

            // Only add if we have valid coordinates and ID
            if (!string.IsNullOrEmpty(geoPoint.lat) &&
                !string.IsNullOrEmpty(geoPoint.lon) &&
                !string.IsNullOrEmpty(geoPoint.id) &&
                !(geoPoint.lat == "0" && geoPoint.lon == "0"))
            {
                geoPoints.Add(geoPoint);
            }
        }

        /// <summary>
        /// Finds a node by ID within the elements array (local search)
        /// </summary>
    private static JsonElement? FindNodeInElements(JsonElement elements, long nodeId)
        {
            foreach (var element in elements.EnumerateArray())
            {
                if (element.TryGetProperty("type", out var typeProperty) &&
                    typeProperty.GetString() == "node" &&
                    element.TryGetProperty("id", out var idProperty) &&
                    idProperty.GetInt64() == nodeId)
                {
                    return element;
                }
            }
            return null;
        }

        /// <summary>
        /// Fetches multiple nodes in batch using Overpass API
        /// </summary>
    private async Task<Dictionary<long, JsonElement>> FetchNodesBatchAsync(List<long> nodeIds)
        {
            var result = new Dictionary<long, JsonElement>();

            if (nodeIds.Count == 0)
                return result;

            try
            {
                // Create Overpass query for batch node retrieval
                var nodeIdsList = string.Join(",", nodeIds);
                var overpassQuery = $@"
                    [out:json];
                    (
                      node(id:{nodeIdsList});
                    );
                    out geom;
                ";

                var client = _clientFactory.CreateClient();
                var url = "https://overpass-api.de/api/interpreter";

                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("data", overpassQuery)
                };

                var formContent = new FormUrlEncodedContent(formData);
                var response = await client.PostAsync(url, formContent);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Batch node fetch failed with status {Status}", response.StatusCode);
                    return result;
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(responseText);

                if (jsonDoc.TryGetProperty("elements", out var elements))
                {
                    foreach (var element in elements.EnumerateArray())
                    {
                        if (element.TryGetProperty("type", out var typeProperty) &&
                            typeProperty.GetString() == "node" &&
                            element.TryGetProperty("id", out var idProperty))
                        {
                            var nodeId = idProperty.GetInt64();
                            result[nodeId] = element;
                        }
                    }
                }

                Log.Debug("Fetched {Fetched}/{Requested} nodes in batch", result.Count, nodeIds.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in batch node fetch");
            }

            return result;
        }

        /// <summary>
        /// Legacy method kept for compatibility - now searches locally first
        /// </summary>
        private static JsonElement? FindNodeById(JsonElement elements, long nodeId)
        {
            return FindNodeInElements(elements, nodeId);
        }
    }
}