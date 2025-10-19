using AngleSharp.Html.Parser;
using Serilog;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using prepareBikeParking.Logging;

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

            var stopwatch = Stopwatch.StartNew();
            var logger = Log.Logger.ForOperation("FetchFromApi");
            logger.Information("Fetching bike share data. Url: {Url}", url);

            try
            {
                var client = _clientFactory.CreateClient();
                var fetchedJson = await client.GetStringAsync(url);
                var fetchDuration = stopwatch.ElapsedMilliseconds;
                logger.LogApiCall("GBFS", url, 200, fetchDuration);
                var parsedJson = JsonSerializer.Deserialize<JsonElement>(fetchedJson);
                if (!parsedJson.TryGetProperty("data", out var dataNode) ||
                    !dataNode.TryGetProperty("stations", out var stations) ||
                    stations.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("GBFS station feed missing data.stations array");
                }

                var locationList = new List<GeoPoint>();
                var skippedCount = 0;
                var invalidCount = 0;

                foreach (var x in stations.EnumerateArray())
                {
                    try
                    {
                        string id = x.TryGetProperty("station_id", out var idProp) ? (idProp.GetString() ?? string.Empty) : string.Empty;
                        string name = x.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? string.Empty) : string.Empty;
                        int capacity = x.TryGetProperty("capacity", out var capProp) && capProp.ValueKind == JsonValueKind.Number ? capProp.GetInt32() : 0;
                        string lat = x.TryGetProperty("lat", out var latProp) && latProp.ValueKind == JsonValueKind.Number ? latProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
                        string lon = x.TryGetProperty("lon", out var lonProp) && lonProp.ValueKind == JsonValueKind.Number ? lonProp.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";

                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || lat == "0" || lon == "0")
                        {
                            invalidCount++;
                            logger.LogDataQualityIssue("InvalidStation", "Missing required fields", new { id, name, lat, lon });
                            continue;
                        }
                        locationList.Add(new GeoPoint { id = id, name = name, capacity = capacity, lat = lat, lon = lon });
                    }
                    catch (Exception exInner)
                    {
                        skippedCount++;
                        logger.Warning(exInner, "Skipping malformed station entry. Error: {Error}", exInner.Message);
                    }
                }

                var result = locationList.ToList();
                stopwatch.Stop();

                var metrics = new LoggingMetrics.DataProcessingMetrics
                {
                    ItemsProcessed = result.Count,
                    ItemsFailed = invalidCount,
                    ItemsSkipped = skippedCount,
                    TotalProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };

                logger.Information("GBFS fetch completed. Stations: {StationCount}, Invalid: {InvalidCount}, Skipped: {SkippedCount}, Duration: {DurationMs}ms",
                    result.Count, invalidCount, skippedCount, stopwatch.ElapsedMilliseconds);
                logger.LogPerformanceMetric("GbfsFetchDuration", stopwatch.ElapsedMilliseconds, "ms");

                return result;
            }
            catch (HttpRequestException httpEx)
            {
                stopwatch.Stop();
                logger.LogApiCall("GBFS", url, 0, stopwatch.ElapsedMilliseconds);
                logger.Error(httpEx, "HTTP request failed. Url: {Url}, Duration: {DurationMs}ms", url, stopwatch.ElapsedMilliseconds);
                throw new Exception($"Failed to fetch bike share data from {url}: {httpEx.Message}", httpEx);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.Error(ex, "Failed to fetch bike share data. Url: {Url}, Duration: {DurationMs}ms", url, stopwatch.ElapsedMilliseconds);
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
            if (displaylist == null)
            {
                throw new InvalidOperationException("Could not find element 'infoWind' in HTML response");
            }

            var namesAndCapacities = displaylist.Children.Select(x => new
            {
                id = x.GetAttribute("id"),
                name = x.Children[0].TextContent.Trim(),
                aproxCapacity = x.Children.Length == 4 ?
                        IntParseOrZero(x.Children[1].TextContent) +
                        IntParseOrZero(x.Children[3].TextContent) +
                        IntParseOrZero(x.Children[2].TextContent) : 0,
            });

            var locationsElement = document.GetElementById("arr_adr");
            if (locationsElement == null)
            {
                throw new InvalidOperationException("Could not find element 'arr_adr' in HTML response");
            }

            var locations = locationsElement.GetAttribute("value");
            if (locations == null)
            {
                throw new InvalidOperationException("Element 'arr_adr' has no 'value' attribute");
            }

            // Parse JSON
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(locations);
            if (json == null)
            {
                throw new InvalidOperationException("Failed to parse locations JSON");
            }

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
