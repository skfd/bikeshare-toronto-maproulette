using AngleSharp.Html.Parser;
using Serilog;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace prepareBikeParking
{
    public interface IHttpClientFactoryShim
    {
        HttpClient CreateClient();
    }

    public class DefaultHttpClientFactoryShim : IHttpClientFactoryShim
    {
        public HttpClient CreateClient() => new HttpClient();
    }

    public class BikeShareDataFetcher
    {
        private readonly IHttpClientFactoryShim _clientFactory;

        public BikeShareDataFetcher() : this(new DefaultHttpClientFactoryShim()) {}
        public BikeShareDataFetcher(IHttpClientFactoryShim clientFactory)
        {
            _clientFactory = clientFactory;
        }
        /// <summary>
        /// Fetches bike share locations from the official GBFS API
        /// </summary>
        /// <param name="apiUrl">Optional custom API URL. If not provided, defaults to Toronto's API.</param>
    public async Task<List<GeoPoint>> FetchFromApiAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("API URL must be provided", nameof(url));
            Log.Information("Fetching bike share data from {Url}", url);

            try
            {
                var client = _clientFactory.CreateClient();
                var fetchedJson = await client.GetStringAsync(url);
                var parsedJson = JsonSerializer.Deserialize<JsonElement>(fetchedJson);
                if (!parsedJson.TryGetProperty("data", out var dataNode) ||
                    !dataNode.TryGetProperty("stations", out var stations) ||
                    stations.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("GBFS station feed missing data.stations array");
                }

                var locationList = new List<GeoPoint>();
                foreach (var x in stations.EnumerateArray())
                {
                    try
                    {
                        string id = x.TryGetProperty("station_id", out var idProp) ? (idProp.GetString() ?? string.Empty) : string.Empty;
                        string name = x.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? string.Empty) : string.Empty;
                        int capacity = x.TryGetProperty("capacity", out var capProp) && capProp.ValueKind == JsonValueKind.Number ? capProp.GetInt32() : 0;
                        string lat = x.TryGetProperty("lat", out var latProp) && latProp.ValueKind == JsonValueKind.Number ? latProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
                        string lon = x.TryGetProperty("lon", out var lonProp) && lonProp.ValueKind == JsonValueKind.Number ? lonProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || lat == "0" || lon == "0") continue;
                        locationList.Add(new GeoPoint { id = id, name = name, capacity = capacity, lat = lat, lon = lon });
                    }
                    catch (Exception exInner)
                    {
                        Log.Warning(exInner, "Skipping malformed station entry in GBFS feed");
                    }
                }

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
