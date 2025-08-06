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
                // Only process nodes that have bicycle_rental=docking_station
                if (element.TryGetProperty("type", out var typeProperty) && 
                    typeProperty.GetString() == "node" &&
                    element.TryGetProperty("tags", out var tagsProperty))
                {
                    var tags = tagsProperty;
                    
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
            }

            return geoPoints;
        }
    }
}