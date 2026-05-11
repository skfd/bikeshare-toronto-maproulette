using System.Net;
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
        private static readonly HttpClient _shared = CreateConfiguredClient();

        private static HttpClient CreateConfiguredClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(180)
            };
            // overpass-api.de's Apache front-end returns 406 Not Acceptable for
            // requests missing a User-Agent or a matching Accept header.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "bikeshare-toronto-maproulette/1.0 (+https://github.com/skfd/bikeshare-toronto-maproulette)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*;q=0.8");
            return client;
        }

        public HttpClient CreateClient() => _shared;
    }

    public class OSMDataFetcher
    {
        // Default community-known Overpass mirrors, in priority order.
        public static readonly IReadOnlyList<string> DefaultEndpoints = new[]
        {
            "https://overpass-api.de/api/interpreter",
            "https://overpass.kumi.systems/api/interpreter",
            "https://overpass.private.coffee/api/interpreter",
            "https://overpass.osm.ch/api/interpreter",
        };

        private static readonly Lazy<IReadOnlyList<string>> _resolvedEndpoints =
            new(ResolveEndpointsFromEnv);

        private static IReadOnlyList<string> ResolveEndpointsFromEnv()
        {
            var env = Environment.GetEnvironmentVariable("OVERPASS_API_URL");
            if (string.IsNullOrWhiteSpace(env)) return DefaultEndpoints;
            var parts = env.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            return parts.Count == 0 ? DefaultEndpoints : parts;
        }

        private readonly IOverpassHttpClientFactory _clientFactory;
        private readonly IReadOnlyList<string> _endpoints;
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseRetryDelay;
        private readonly TimeSpan _maxRetryDelay;
        private readonly Random _rng = new Random();

        public OSMDataFetcher() : this(new DefaultOverpassHttpClientFactory()) {}
        public OSMDataFetcher(IOverpassHttpClientFactory clientFactory)
            : this(clientFactory, endpoints: null, maxAttempts: 4, baseRetryDelay: TimeSpan.FromSeconds(2)) {}
        public OSMDataFetcher(
            IOverpassHttpClientFactory clientFactory,
            IReadOnlyList<string>? endpoints,
            int maxAttempts,
            TimeSpan baseRetryDelay)
        {
            _clientFactory = clientFactory;
            _endpoints = endpoints ?? _resolvedEndpoints.Value;
            _maxAttempts = Math.Max(1, maxAttempts);
            _baseRetryDelay = baseRetryDelay;
            _maxRetryDelay = TimeSpan.FromSeconds(30);
        }
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
                ConsoleUI.PrintWarning($"stations.overpass file not found for {systemName}; using fallback query.");
                ConsoleUI.PrintAction($"Create data_results/{systemName}/stations.overpass to customize the Overpass query.");
                overpassQuery = GenerateDefaultOverpassQuery(systemName);
            }

            var responseText = await PostOverpassQueryWithRetryAsync(overpassQuery);
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
                ConsoleUI.PrintSuccess($"Created default stations.overpass for {systemName}.");
                ConsoleUI.PrintAction($"Edit data_results/{systemName}/stations.overpass to customize the Overpass query.");
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
            var wayElements = new List<(JsonElement element, JsonElement tags, long firstNodeId, bool isDisused)>();

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
                    // Check if it's a bikeshare station (active or disused)
                    var isDockingStation = tags.TryGetProperty("bicycle_rental", out var rentalProp) &&
                        rentalProp.GetString() == "docking_station";
                    var isDisused = IsDisusedStation(tags);

                    if (isDockingStation || isDisused)
                    {
                        var geoPoint = new GeoPoint
                        {
                            id = string.Empty,
                            name = string.Empty,
                            lat = "0",
                            lon = "0",
                            IsDisused = isDisused,
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

                        if (tags.TryGetProperty("ref:gbfs", out var refGbfsProp))
                        {
                            geoPoint.RefGbfs = refGbfsProp.GetString();
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
                    // Check if it's a bikeshare station (active or disused)
                    var isDockingStation = tags.TryGetProperty("bicycle_rental", out var rentalProp) &&
                        rentalProp.GetString() == "docking_station";
                    var isDisused = IsDisusedStation(tags);

                    if (isDockingStation || isDisused)
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
                                    ProcessWayElement(element, tags, nodeElement.Value, geoPoints, isDisused);
                                }
                                else
                                {
                                    // Store for batch retrieval
                                    missingNodeIds.Add(firstNodeId);
                                    wayElements.Add((element, tags, firstNodeId, isDisused));
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
            foreach (var (element, tags, firstNodeId, isDisused) in wayElements)
            {
                if (fetchedNodes.TryGetValue(firstNodeId, out var nodeElement))
                {
                    ProcessWayElement(element, tags, nodeElement, geoPoints, isDisused);
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
        /// Validates OSM data for duplicate ref and ref:gbfs values and generates a report if found
        /// </summary>
        private static async Task ValidateAndReportDuplicates(List<GeoPoint> geoPoints, string systemName)
        {
            // Group by ref ID, excluding auto-generated IDs (those starting with "osm_")
            var refDuplicates = geoPoints
                .Where(p => !string.IsNullOrEmpty(p.id) && !p.id.StartsWith("osm_"))
                .GroupBy(p => p.id)
                .Where(g => g.Count() > 1)
                .ToList();

            var refGbfsDuplicates = geoPoints
                .Where(p => !string.IsNullOrWhiteSpace(p.RefGbfs))
                .GroupBy(p => p.RefGbfs!)
                .Where(g => g.Count() > 1)
                .ToList();

            if (!refDuplicates.Any() && !refGbfsDuplicates.Any())
            {
                Log.Debug("No duplicate ref or ref:gbfs values found in OSM data for {System}", systemName);
                return;
            }

            var totalAffected = refDuplicates.SelectMany(g => g).Count() + refGbfsDuplicates.SelectMany(g => g).Count();

            Log.Warning("Found {RefDups} duplicate ref value(s) and {GbfsDups} duplicate ref:gbfs value(s) in OSM data for {System}. " +
                       "Total affected entries: {Affected}. " +
                       "See data_results/{System}/bikeshare_osm_duplicates.geojson for details",
                       refDuplicates.Count, refGbfsDuplicates.Count, systemName, totalAffected, systemName);
            ConsoleUI.PrintWarning($"Found {refDuplicates.Count} duplicate ref + {refGbfsDuplicates.Count} duplicate ref:gbfs in OSM ({totalAffected} affected entries).");
            ConsoleUI.PrintAction($"Review data_results/{systemName}/bikeshare_osm_duplicates.geojson and fix duplicates in OSM.");

            foreach (var dup in refDuplicates)
            {
                var osmDetails = string.Join(", ", dup.Select(d => $"{d.osmType}/{d.osmId}"));
                Log.Warning("  Duplicate ref '{Ref}' found in {Count} OSM elements: {OsmIds}",
                    dup.Key, dup.Count(), osmDetails);
            }
            foreach (var dup in refGbfsDuplicates)
            {
                var osmDetails = string.Join(", ", dup.Select(d => $"{d.osmType}/{d.osmId}"));
                Log.Warning("  Duplicate ref:gbfs '{Ref}' found in {Count} OSM elements: {OsmIds}",
                    dup.Key, dup.Count(), osmDetails);
            }

            var refCounts = refDuplicates.ToDictionary(g => g.Key, g => g.Count());
            var refGbfsCounts = refGbfsDuplicates.ToDictionary(g => g.Key, g => g.Count());

            var lines = new List<string>();
            foreach (var dup in refDuplicates)
            {
                foreach (var point in dup)
                {
                    lines.Add(GeoJsonGenerator.GenerateGeojsonLineWithError(
                        point,
                        systemName,
                        $"Duplicate ref '{point.id}' appears {refCounts[point.id]} times in OSM (this is OSM {point.osmType}/{point.osmId})"));
                }
            }
            foreach (var dup in refGbfsDuplicates)
            {
                foreach (var point in dup)
                {
                    lines.Add(GeoJsonGenerator.GenerateGeojsonLineWithError(
                        point,
                        systemName,
                        $"Duplicate ref:gbfs '{point.RefGbfs}' appears {refGbfsCounts[point.RefGbfs!]} times in OSM (this is OSM {point.osmType}/{point.osmId})"));
                }
            }

            try
            {
                await FileManager.WriteSystemLinesAsync(systemName, "bikeshare_osm_duplicates.geojson", lines);
                Log.Information("Duplicate validation report saved to data_results/{System}/bikeshare_osm_duplicates.geojson", systemName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not save duplicate validation report for {System}", systemName);
            }
        }

        /// <summary>
        /// Generates an enhanced duplicate report that includes GBFS data for comparison.
        /// Detects duplicates on both `ref` and `ref:gbfs`.
        /// </summary>
        /// <param name="osmPoints">OSM data points that may contain duplicates</param>
        /// <param name="gbfsPoints">GBFS/API data points for reference</param>
        /// <param name="systemName">Name of the bike share system</param>
        public static async Task GenerateEnhancedDuplicateReportAsync(List<GeoPoint> osmPoints, List<GeoPoint> gbfsPoints, string systemName)
        {
            var refDuplicates = osmPoints
                .Where(p => !string.IsNullOrEmpty(p.id) && !p.id.StartsWith("osm_"))
                .GroupBy(p => p.id)
                .Where(g => g.Count() > 1)
                .ToList();

            var refGbfsDuplicates = osmPoints
                .Where(p => !string.IsNullOrWhiteSpace(p.RefGbfs))
                .GroupBy(p => p.RefGbfs!)
                .Where(g => g.Count() > 1)
                .ToList();

            if (!refDuplicates.Any() && !refGbfsDuplicates.Any())
            {
                Log.Debug("No duplicate ref or ref:gbfs values found - enhanced report not needed");
                return;
            }

            var refCounts = refDuplicates.ToDictionary(g => g.Key, g => g.Count());
            var refGbfsCounts = refGbfsDuplicates.ToDictionary(g => g.Key, g => g.Count());
            var lines = new List<string>();

            foreach (var dup in refDuplicates)
            {
                foreach (var osmStation in dup)
                {
                    lines.Add(GeoJsonGenerator.GenerateGeojsonLineWithError(
                        osmStation,
                        systemName,
                        $"Duplicate ref '{osmStation.id}' appears {refCounts[osmStation.id]} times in OSM (this is OSM {osmStation.osmType}/{osmStation.osmId})"));
                }

                var gbfsStation = gbfsPoints.FirstOrDefault(g => g.id == dup.Key);
                if (gbfsStation != null)
                {
                    var gbfsForComparison = new GeoPoint
                    {
                        id = gbfsStation.id,
                        name = gbfsStation.name,
                        lat = gbfsStation.lat,
                        lon = gbfsStation.lon,
                        capacity = gbfsStation.capacity,
                        osmType = "GBFS",
                        osmId = "official",
                        osmVersion = 0,
                        osmXmlElement = default(JsonElement)
                    };
                    lines.Add(GeoJsonGenerator.GenerateGeojsonLineWithError(
                        gbfsForComparison,
                        systemName,
                        $"OFFICIAL GBFS DATA for ref '{dup.Key}' - Compare with OSM duplicates above to verify which is correct"));
                }
            }

            foreach (var dup in refGbfsDuplicates)
            {
                foreach (var osmStation in dup)
                {
                    lines.Add(GeoJsonGenerator.GenerateGeojsonLineWithError(
                        osmStation,
                        systemName,
                        $"Duplicate ref:gbfs '{osmStation.RefGbfs}' appears {refGbfsCounts[osmStation.RefGbfs!]} times in OSM (this is OSM {osmStation.osmType}/{osmStation.osmId})"));
                }
            }

            try
            {
                await FileManager.WriteSystemLinesAsync(systemName, "bikeshare_osm_duplicates.geojson", lines);
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
    private static void ProcessWayElement(JsonElement element, JsonElement tags, JsonElement nodeElement, List<GeoPoint> geoPoints, bool isDisused)
        {
            var geoPoint = new GeoPoint
            {
                id = string.Empty,
                name = string.Empty,
                lat = "0",
                lon = "0",
                IsDisused = isDisused,
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

            if (tags.TryGetProperty("ref:gbfs", out var refGbfsProp))
            {
                geoPoint.RefGbfs = refGbfsProp.GetString();
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
        /// Checks if an OSM element's tags indicate a temporarily disused station.
        /// A station is considered disused if it has the tag disused:amenity=bicycle_rental.
        /// </summary>
        private static bool IsDisusedStation(JsonElement tags)
        {
            return tags.TryGetProperty("disused:amenity", out var disusedProp) &&
                   disusedProp.GetString() == "bicycle_rental";
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

            // Create Overpass query for batch node retrieval
            var nodeIdsList = string.Join(",", nodeIds);
            var overpassQuery = $@"
                [out:json];
                (
                  node(id:{nodeIdsList});
                );
                out geom;
            ";

            var responseText = await PostOverpassQueryWithRetryAsync(overpassQuery);
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
            return result;
        }

        /// <summary>
        /// Legacy method kept for compatibility - now searches locally first
        /// </summary>
        private static JsonElement? FindNodeById(JsonElement elements, long nodeId)
        {
            return FindNodeInElements(elements, nodeId);
        }

        /// <summary>
        /// POSTs an Overpass query, retrying transient failures with exponential backoff
        /// and failing over across the configured endpoint list.
        /// </summary>
        private async Task<string> PostOverpassQueryWithRetryAsync(string overpassQuery)
        {
            var client = _clientFactory.CreateClient();
            Exception? lastError = null;

            for (int epIdx = 0; epIdx < _endpoints.Count; epIdx++)
            {
                var endpoint = _endpoints[epIdx];
                var host = SafeHost(endpoint);

                for (int attempt = 1; attempt <= _maxAttempts; attempt++)
                {
                    HttpResponseMessage? response = null;
                    try
                    {
                        var formContent = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("data", overpassQuery)
                        });
                        response = await client.PostAsync(endpoint, formContent);

                        if (response.IsSuccessStatusCode)
                        {
                            if (attempt > 1 || epIdx > 0)
                            {
                                Log.Information("Overpass request succeeded at {Endpoint} on attempt {Attempt}", endpoint, attempt);
                            }
                            return await response.Content.ReadAsStringAsync();
                        }

                        var status = (int)response.StatusCode;
                        if (status == 429)
                        {
                            var delay = ComputeRetryAfterDelay(response.Headers.RetryAfter, attempt);
                            var body = await SafeReadAsync(response);
                            lastError = new Exception($"Overpass API rate-limited at {endpoint} (429): {Truncate(body, 200)}");
                            if (attempt < _maxAttempts)
                            {
                                Log.Warning("Overpass {Host} returned 429 (attempt {Attempt}/{Max}); waiting {Delay}s before retry",
                                    host, attempt, _maxAttempts, delay.TotalSeconds);
                                ConsoleUI.PrintWarning($"Overpass {host}: rate-limited (429). Retrying in {delay.TotalSeconds:F0}s (attempt {attempt}/{_maxAttempts}).");
                                await Task.Delay(delay);
                                continue;
                            }
                        }
                        else if (status >= 500 && status <= 599)
                        {
                            var delay = BackoffDelay(attempt);
                            var body = await SafeReadAsync(response);
                            lastError = new Exception($"Overpass API failed at {endpoint}: {response.StatusCode} - {Truncate(body, 200)}");
                            if (attempt < _maxAttempts)
                            {
                                Log.Warning("Overpass {Host} returned {Status} (attempt {Attempt}/{Max}); waiting {Delay}s before retry. Body: {Body}",
                                    host, status, attempt, _maxAttempts, delay.TotalSeconds, Truncate(body, 200));
                                ConsoleUI.PrintWarning($"Overpass {host}: server error {status}. Retrying in {delay.TotalSeconds:F0}s (attempt {attempt}/{_maxAttempts}).");
                                await Task.Delay(delay);
                                continue;
                            }
                        }
                        else
                        {
                            // 4xx other than 429 — treat as a query-level error and fail fast.
                            var body = await SafeReadAsync(response);
                            throw new Exception($"Overpass API request failed: {response.StatusCode} - {body}");
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        lastError = ex;
                        var delay = BackoffDelay(attempt);
                        if (attempt < _maxAttempts)
                        {
                            Log.Warning(ex, "Overpass {Host} network error (attempt {Attempt}/{Max}); waiting {Delay}s before retry",
                                host, attempt, _maxAttempts, delay.TotalSeconds);
                            ConsoleUI.PrintWarning($"Overpass {host}: network error. Retrying in {delay.TotalSeconds:F0}s (attempt {attempt}/{_maxAttempts}).");
                            await Task.Delay(delay);
                        }
                    }
                    catch (TaskCanceledException ex)
                    {
                        lastError = ex;
                        var delay = BackoffDelay(attempt);
                        if (attempt < _maxAttempts)
                        {
                            Log.Warning(ex, "Overpass {Host} timed out (attempt {Attempt}/{Max}); waiting {Delay}s before retry",
                                host, attempt, _maxAttempts, delay.TotalSeconds);
                            ConsoleUI.PrintWarning($"Overpass {host}: timed out. Retrying in {delay.TotalSeconds:F0}s (attempt {attempt}/{_maxAttempts}).");
                            await Task.Delay(delay);
                        }
                    }
                    finally
                    {
                        response?.Dispose();
                    }
                }

                if (epIdx + 1 < _endpoints.Count)
                {
                    Log.Warning("Overpass endpoint {Host} exhausted after {Max} attempts; trying next endpoint", host, _maxAttempts);
                    ConsoleUI.PrintWarning($"Overpass {host} exhausted; trying next endpoint.");
                }
            }

            throw lastError ?? new Exception("All Overpass endpoints exhausted");
        }

        private TimeSpan BackoffDelay(int attempt)
        {
            // Exponential backoff with ±20% jitter, capped at _maxRetryDelay.
            var raw = _baseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
            var capped = Math.Min(raw, _maxRetryDelay.TotalMilliseconds);
            var jitterFactor = 1.0 + ((_rng.NextDouble() * 0.4) - 0.2);
            return TimeSpan.FromMilliseconds(capped * jitterFactor);
        }

        private TimeSpan ComputeRetryAfterDelay(System.Net.Http.Headers.RetryConditionHeaderValue? retryAfter, int attempt)
        {
            var backoff = BackoffDelay(attempt);
            if (retryAfter == null) return backoff;
            TimeSpan? hinted = retryAfter.Delta
                ?? (retryAfter.Date.HasValue ? retryAfter.Date.Value - DateTimeOffset.UtcNow : (TimeSpan?)null);
            if (hinted == null || hinted.Value <= TimeSpan.Zero) return backoff;
            var clamped = hinted.Value > _maxRetryDelay ? _maxRetryDelay : hinted.Value;
            return clamped > backoff ? clamped : backoff;
        }

        private static async Task<string> SafeReadAsync(HttpResponseMessage response)
        {
            try { return await response.Content.ReadAsStringAsync(); }
            catch { return string.Empty; }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

        private static string SafeHost(string url)
        {
            try { return new Uri(url).Host; }
            catch { return url; }
        }
    }
}