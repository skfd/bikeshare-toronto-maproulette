using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using System.IO;

namespace prepareBikeParking
{
    public static class GlobalGbfsServiceFetcher
    {
        private const string GlobalGbfsUrl = "https://gbfs.org/gbfs-versions.json";
        private const string ProvidersListUrl = "https://gbfs.org/gbfs.json";

        public static async Task<string> FetchGlobalServiceListAsync()
        {
            using var client = new HttpClient();
            Log.Information("Fetching global GBFS service provider list from {Url}", ProvidersListUrl);
            var json = await client.GetStringAsync(ProvidersListUrl);
            return json;
        }

        public static async Task SaveGlobalServiceListAsync(string filePath)
        {
            var json = await FetchGlobalServiceListAsync();
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(filePath, json);
            Log.Information("Saved global GBFS service provider list to {Path}", filePath);
        }
    }
}
