using System.Text.Json;

namespace prepareBikeParking
{
    public static class OSMDataFetcher
    {
        /// <summary>
        /// Fetches bikeshare station data from OpenStreetMap using system-specific Overpass query file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        public static async Task<List<GeoPoint>> FetchFromOverpassApiAsync(string systemName)
        {
            // Try to load system-specific overpass query first
            string overpassQuery;
            try
            {
                overpassQuery = await FileManager.ReadSystemTextFileAsync(systemName, "stations.overpass");
                Console.WriteLine($"✅ Using system-specific Overpass query from data_results/{systemName}/stations.overpass");
            }
            catch (FileNotFoundException)
            {
                // Fallback to default query generation for backward compatibility
                Console.WriteLine($"⚠️  System-specific stations.overpass file not found for {systemName}");
                Console.WriteLine("   Using fallback query generation. Consider creating a stations.overpass file for better control.");
                overpassQuery = GenerateDefaultOverpassQuery(systemName);
            }

            var url = "https://overpass-api.de/api/interpreter";
            var client = new HttpClient();

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

            return await ParseOverpassResponseAsync(responseText);
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
                Console.WriteLine($"✅ Created default stations.overpass file for {systemName}");
                Console.WriteLine($"   Edit data_results/{systemName}/stations.overpass to customize the Overpass query");
            }
        }

        /// <summary>
        /// Parses the Overpass API JSON response into GeoPoint objects
        /// </summary>
        private static async Task<List<GeoPoint>> ParseOverpassResponseAsync(string jsonResponse)
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
                        var geoPoint = new GeoPoint();
                        geoPoint.osmId = element.GetProperty("id").GetInt64().ToString();
                        geoPoint.osmType = type;
                        geoPoint.osmVersion = element.GetProperty("version").GetInt32();
                        geoPoint.osmXmlElement = element;

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
                            !string.IsNullOrEmpty(geoPoint.id))
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
                Console.WriteLine($"Fetching {missingNodeIds.Count} missing nodes in batch...");
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
                    Console.WriteLine($"Warning: Could not fetch coordinates for node {firstNodeId}");
                }
            }

            return geoPoints;
        }

        /// <summary>
        /// Processes a way element and adds it to the geoPoints list
        /// </summary>
        private static void ProcessWayElement(JsonElement element, JsonElement tags, JsonElement nodeElement, List<GeoPoint> geoPoints)
        {
            var geoPoint = new GeoPoint();

            geoPoint.osmId = element.GetProperty("id").GetInt64().ToString();
            geoPoint.osmType = "way";
            geoPoint.osmVersion = element.GetProperty("version").GetInt32();
            geoPoint.osmXmlElement = element;

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
                !string.IsNullOrEmpty(geoPoint.id))
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
        private static async Task<Dictionary<long, JsonElement>> FetchNodesBatchAsync(List<long> nodeIds)
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

                using var client = new HttpClient();
                var url = "https://overpass-api.de/api/interpreter";
                
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("data", overpassQuery)
                };

                var formContent = new FormUrlEncodedContent(formData);
                var response = await client.PostAsync(url, formContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Batch node fetch failed: {response.StatusCode}");
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
                
                Console.WriteLine($"Successfully fetched {result.Count}/{nodeIds.Count} nodes in batch");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in batch node fetch: {ex.Message}");
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