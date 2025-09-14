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

        /// <summary>
        /// Gets the station_information endpoint URL from the GBFS API base URL
        /// </summary>
        public string GetStationInformationUrl()
        {
            return GbfsApi;
        }
    }
}