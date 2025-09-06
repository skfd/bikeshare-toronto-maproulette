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
            // Handle direct station_information URLs
            if (GbfsApi.EndsWith("station_information") || GbfsApi.EndsWith("station_information.json"))
            {
                return GbfsApi;
            }
            
            // Handle base GBFS URLs that point to gbfs.json
            if (GbfsApi.EndsWith("gbfs.json"))
            {
                // For URLs like "https://gbfs.velobixi.com/gbfs/2-2/gbfs.json"
                // we need to construct the station_information URL
                var urlParts = GbfsApi.Split('/');
                var basePath = string.Join("/", urlParts.Take(urlParts.Length - 1));
                return $"{basePath}/station_information.json";
            }
            
            // Handle base GBFS directory URLs
            var baseUrl = GbfsApi.TrimEnd('/');
            
            // For URLs like "https://tor.publicbikesystem.net/ube/gbfs/v1/en/"
            // append station_information
            return $"{baseUrl}/station_information";
        }
    }
}