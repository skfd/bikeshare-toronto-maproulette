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
        /// Fetches bike share locations from the official GBFS station_information feed.
        /// </summary>
        public Task<List<GeoPoint>> FetchFromApiAsync(string? url) => FetchFromApiAsync(url, null);

        /// <summary>
        /// Fetches bike share locations from the official GBFS station_information feed and, when a
        /// station_status URL is supplied, marks closed stations (decommissioned or temporarily out of
        /// service) via <see cref="GeoPoint.IsClosed"/>. A failure fetching/parsing the status feed is a
        /// soft error: it is logged and the run continues with no stations flagged closed.
        /// </summary>
    public async Task<List<GeoPoint>> FetchFromApiAsync(string? url, string? statusUrl)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("API URL must be provided", nameof(url));

            var stopwatch = Stopwatch.StartNew();
            var logger = Log.Logger.ForOperation("FetchFromApi");
            logger.Information("Fetching bike share data. Url: {Url}", url);

            try
            {
                var client = _clientFactory.CreateClient();
                var infoTask = client.GetStringAsync(url);
                Task<string>? statusTask = string.IsNullOrWhiteSpace(statusUrl) ? null : client.GetStringAsync(statusUrl);

                string fetchedJson;
                try
                {
                    fetchedJson = await infoTask;
                }
                catch
                {
                    // Observe the status task so it doesn't surface as an unobserved exception, then rethrow.
                    if (statusTask != null) { try { await statusTask; } catch { /* ignored */ } }
                    throw;
                }
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

                if (statusTask != null)
                {
                    try
                    {
                        var statusJson = await statusTask;
                        ApplyStationStatus(result, statusJson, statusUrl!, logger);
                    }
                    catch (Exception statusEx)
                    {
                        logger.Warning(statusEx, "station_status fetch/parse failed. Url: {Url}. Proceeding without closed-station detection.", statusUrl);
                    }
                }

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
        /// Marks stations closed based on a GBFS station_status feed. A station is "closed" when its
        /// status entry reports it is not installed (decommissioned) or installed but not renting/returning
        /// (temporarily out of service). Stations with no matching status entry are left unchanged.
        /// </summary>
        private static void ApplyStationStatus(List<GeoPoint> stations, string statusJson, string statusUrl, Serilog.ILogger logger)
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(statusJson);
            if (!parsed.TryGetProperty("data", out var dataNode) ||
                !dataNode.TryGetProperty("stations", out var statusStations) ||
                statusStations.ValueKind != JsonValueKind.Array)
            {
                logger.Warning("station_status feed missing data.stations array. Url: {Url}. Proceeding without closed-station detection.", statusUrl);
                return;
            }

            var statusById = new Dictionary<string, (bool installed, bool renting, bool returning)>();
            foreach (var s in statusStations.EnumerateArray())
            {
                if (!s.TryGetProperty("station_id", out var idProp)) continue;
                var id = idProp.GetString();
                if (string.IsNullOrEmpty(id)) continue;
                statusById[id] = (
                    TryGetGbfsBool(s, "is_installed", true),
                    TryGetGbfsBool(s, "is_renting", true),
                    TryGetGbfsBool(s, "is_returning", true));
            }

            int closedCount = 0, decommissionedCount = 0, outOfServiceCount = 0, unmatched = 0;
            foreach (var station in stations)
            {
                if (!statusById.TryGetValue(station.id, out var flags))
                {
                    unmatched++;
                    continue;
                }
                var isClosed = !flags.installed || !flags.renting || !flags.returning;
                station.IsClosed = isClosed;
                if (isClosed)
                {
                    closedCount++;
                    if (!flags.installed) decommissionedCount++; else outOfServiceCount++;
                }
            }

            logger.Information("Applied station_status: {Closed} closed station(s) flagged ({Decommissioned} not-installed, {OutOfService} out-of-service); {Unmatched} station_information entr(y/ies) had no matching status entry.",
                closedCount, decommissionedCount, outOfServiceCount, unmatched);
        }

        /// <summary>
        /// Reads a GBFS boolean field that may be encoded as a JSON boolean (v2.0+) or as 0/1 (v1.x).
        /// Returns <paramref name="defaultValue"/> when the field is absent or an unexpected type.
        /// </summary>
        private static bool TryGetGbfsBool(JsonElement obj, string propertyName, bool defaultValue)
        {
            if (!obj.TryGetProperty(propertyName, out var prop)) return defaultValue;
            return prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => prop.TryGetInt32(out var n) ? n != 0 : defaultValue,
                JsonValueKind.String => bool.TryParse(prop.GetString(), out var b)
                    ? b
                    : (int.TryParse(prop.GetString(), out var sn) ? sn != 0 : defaultValue),
                _ => defaultValue
            };
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
