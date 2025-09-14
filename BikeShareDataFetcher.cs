using AngleSharp.Html.Parser;
using Serilog;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace prepareBikeParking
{
    public static class BikeShareDataFetcher
    {
        /// <summary>
        /// Fetches bike share locations from the official GBFS API
        /// </summary>
        /// <param name="apiUrl">Optional custom API URL. If not provided, defaults to Toronto's API.</param>
        public static async Task<List<GeoPoint>> FetchFromApiAsync(string? url)
        {
            Log.Information("Fetching bike share data from {Url}", url);

            try
            {
                var client = new HttpClient();
                var fetchedJson = await client.GetStringAsync(url);
                var parsedJson = JsonSerializer.Deserialize<JsonElement>(fetchedJson);

                var stations = parsedJson.GetProperty("data").GetProperty("stations");
                var locationList = stations.EnumerateArray()
                    .Select(x => new GeoPoint
                    {
                        id = x.GetProperty("station_id").GetString(),
                        name = x.GetProperty("name").GetString(),
                        capacity = x.GetProperty("capacity").GetInt32(),
                        lat = x.GetProperty("lat").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        lon = x.GetProperty("lon").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture)
                    });

                var result = locationList.ToList();
                Log.Information("Fetched {Count} bike share stations from {Url}", result.Count, url);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch bike share data from {Url}", url);
                throw new Exception($"Failed to fetch bike share data from {url}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fetches bike share locations from the BikeShare Toronto website (legacy method)
        /// </summary>
        public static async Task<List<GeoPoint>> FetchFromWebsiteAsync()
        {
            var url = "https://bikesharetoronto.com/system-map/";
            var client = new HttpClient();
            var html = await client.GetStringAsync(url);

            // Parse HTML
            var parser = new HtmlParser();
            var document = parser.ParseDocument(html);

            var displaylist = document.GetElementById("infoWind");
            var namesAndCapacities = displaylist.Children.Select(x => new
            {
                id = x.GetAttribute("id"),
                name = x.Children[0].TextContent.Trim(),
                aproxCapacity = x.Children.Length == 4 ?
                        IntParseOrZero(x.Children[1].TextContent) +
                        IntParseOrZero(x.Children[3].TextContent) +
                        IntParseOrZero(x.Children[2].TextContent) : 0,
            });

            var locations = document.GetElementById("arr_adr").GetAttribute("value");

            // Parse JSON
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(locations);
            var locationsDict = json.ToDictionary(x => x.Key, x => x.Value.Split("_"));

            var locationsList = locationsDict.Select(x => new GeoPoint
            {
                id = x.Key,
                name = namesAndCapacities.Single(y => y.id == x.Key).name,
                capacity = namesAndCapacities.Single(y => y.id == x.Key).aproxCapacity,
                lat = x.Value[0],
                lon = x.Value[1]
            }).ToList();

            return locationsList;
        }

        /// <summary>
        /// Reads bike share locations from an existing GeoJSON file
        /// </summary>
        public static async Task<List<GeoPoint>> ReadFromFileAsync(string systemName, string fileName = "bikeshare.geojson")
        {
            if (!FileManager.SystemFileExists(systemName, fileName))
            {
                throw new FileNotFoundException($"File not found: {fileName} for system {systemName}. Please ensure the file exists or use FetchFromApiAsync() instead.");
            }

            return await FileManager.ReadSystemGeoJsonFileAsync(systemName, fileName);
        }

        /// <summary>
        /// Helper method to parse integers from text or return zero if parsing fails
        /// </summary>
        private static int IntParseOrZero(string input)
        {
            var match = Regex.Match(input, @"\d+");
            return int.TryParse(match.Value, out var result) ? result : 0;
        }
    }
}


// Test file
