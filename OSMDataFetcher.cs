using System.Text.Json;

namespace prepareBikeParking
{
    public static class OSMDataFetcher
    {
        /// <summary>
        /// Fetches bikeshare station data from OpenStreetMap using Overpass API
        /// </summary>
        public static async Task<List<GeoPoint>> FetchFromOverpassApiAsync()
        {
            var overpassQuery = @"
                [out:json];

                area[name=""Toronto""]->.to;
                (
                  node(area.to)[bicycle_rental=docking_station];
                  way(area.to)[bicycle_rental=docking_station];
                  relation(area.to)[bicycle_rental=docking_station];
                );

                out body;
                >;
                out skel qt;
                ";

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

            return ParseOverpassResponse(responseText);
        }

        /// <summary>
        /// Parses the Overpass API JSON response into GeoPoint objects
        /// </summary>
        private static List<GeoPoint> ParseOverpassResponse(string jsonResponse)
        {
            var geoPoints = new List<GeoPoint>();

            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
            var elements = jsonDoc.GetProperty("elements");

            foreach (var element in elements.EnumerateArray())
            {
                element.TryGetProperty("type", out var typeProperty);
                var type = typeProperty.GetString();
                element.TryGetProperty("tags", out var tags);
                
                if (type == "node")
                {
                    // Check if it's a bikeshare station
                    if (tags.TryGetProperty("bicycle_rental", out var rentalProp) &&
                        rentalProp.GetString() == "docking_station")
                    {
                        var geoPoint = new GeoPoint();

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
                                
                                // Find the corresponding node element with coordinates
                                var nodeElement = FindNodeById(elements, firstNodeId);
                                if (nodeElement.HasValue)
                                {
                                    var geoPoint = new GeoPoint();

                                    // Get coordinates from the referenced node
                                    if (nodeElement.Value.TryGetProperty("lat", out var latProp))
                                        geoPoint.lat = latProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

                                    if (nodeElement.Value.TryGetProperty("lon", out var lonProp))
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
                            }
                        }
                    }
                }
            }

            return geoPoints;
        }

        /// <summary>
        /// Queries the OpenStreetMap API to fetch a node's longitude and latitude by its ID
        /// </summary>
        /// <param name="nodeId">The OSM node ID to fetch</param>
        /// <returns>JsonElement containing the node data with lat/lon coordinates, or null if not found</returns>
        private static async Task<JsonElement?> FindNodeByIdAsync(long nodeId)
        {
            try
            {
                using var client = new HttpClient();
                
                // Query OSM API for the specific node
                var url = $"https://api.openstreetmap.org/api/0.6/node/{nodeId}.json";
                
                var response = await client.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null; // Node doesn't exist
                    }
                    throw new Exception($"OSM API request failed: {response.StatusCode}");
                }
                
                var responseText = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(responseText);
                
                // Extract the node from the response
                if (jsonDoc.TryGetProperty("elements", out var elementsProperty) &&
                    elementsProperty.ValueKind == JsonValueKind.Array)
                {
                    var nodeArray = elementsProperty.EnumerateArray().ToArray();
                    if (nodeArray.Length > 0)
                    {
                        return nodeArray[0]; // Return the first (and should be only) node
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // Log the error or handle it appropriately
                Console.WriteLine($"Error fetching node {nodeId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Synchronous wrapper for FindNodeByIdAsync - kept for backward compatibility
        /// </summary>
        private static JsonElement? FindNodeById(JsonElement elements, long nodeId)
        {
            return FindNodeByIdAsync(nodeId).GetAwaiter().GetResult();
        }
    }
}