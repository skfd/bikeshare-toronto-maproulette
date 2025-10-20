using System.Text.Json.Serialization;

namespace prepareBikeParking
{
    public class BikeShareSystem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("maproulette_project_id")]
        public int MaprouletteProjectId { get; set; }

        [JsonPropertyName("gbfs_api")]
        public string GbfsApi { get; set; } = string.Empty;

        [JsonPropertyName("brand:wikidata")]
        public string? BrandWikidata { get; set; }

    [JsonPropertyName("station_name_prefix")]
    public string? StationNamePrefix { get; set; }

        [JsonPropertyName("move_threshold_meters")]
        public double? MoveThresholdMeters { get; set; }

        [JsonPropertyName("osm_comparison_threshold_meters")]
        public double? OsmComparisonThresholdMeters { get; set; }

        /// <summary>
        /// Gets the movement threshold in meters for git diff comparison (current vs previous data).
        /// Default: 3 meters
        /// </summary>
        public double GetMoveThresholdMeters() => MoveThresholdMeters ?? 3.0;

        /// <summary>
        /// Gets the movement threshold in meters for OSM comparison (GBFS vs OSM data).
        /// Default: 30 meters (more lenient due to mapping imprecision)
        /// </summary>
        public double GetOsmComparisonThresholdMeters() => OsmComparisonThresholdMeters ?? 30.0;

        /// <summary>
        /// Gets the station_information endpoint URL from the GBFS API base URL
        /// </summary>
        public string GetStationInformationUrl()
        {
            return GbfsApi;
        }
    }
}