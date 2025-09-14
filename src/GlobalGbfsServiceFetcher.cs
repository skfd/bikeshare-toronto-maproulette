using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using System.IO;

namespace prepareBikeParking
{
    public static class GlobalGbfsServiceFetcher
    {
        private const string SystemsCsvUrl = "https://github.com/MobilityData/gbfs/raw/master/systems.csv";

        public static async Task<string> FetchGlobalServiceListAsync()
        {
            using var client = new HttpClient();
            Log.Information("Fetching global GBFS service provider list from {Url}", SystemsCsvUrl);
            var csv = await client.GetStringAsync(SystemsCsvUrl);
            return csv;
        }

        public static async Task SaveGlobalServiceListAsync(string filePath)
        {
            var csv = await FetchGlobalServiceListAsync();
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(filePath, csv);
            Log.Information("Saved global GBFS service provider list to {Path}", filePath);
        }
    }
}
