using System.Text.Json;

namespace prepareBikeParking
{
    public static class BikeShareSystemLoader
    {
        private const string ConfigFilePath = "../../../bikeshare_systems.json";

        /// <summary>
        /// Loads all bike share systems from the configuration file
        /// </summary>
        public static async Task<List<BikeShareSystem>> LoadAllSystemsAsync()
        {
            if (!File.Exists(ConfigFilePath))
            {
                throw new FileNotFoundException($"Configuration file '{ConfigFilePath}' not found. Please ensure the file exists in the application directory.");
            }

            var jsonContent = await File.ReadAllTextAsync(ConfigFilePath);
            var systems = JsonSerializer.Deserialize<List<BikeShareSystem>>(jsonContent);
            
            if (systems == null)
            {
                throw new InvalidOperationException($"Failed to parse configuration file '{ConfigFilePath}'. Please ensure the file contains valid JSON.");
            }

            return systems;
        }

        /// <summary>
        /// Loads a specific bike share system by ID
        /// </summary>
        public static async Task<BikeShareSystem> LoadSystemByIdAsync(int systemId)
        {
            var systems = await LoadAllSystemsAsync();
            var system = systems.FirstOrDefault(s => s.Id == systemId);
            
            if (system == null)
            {
                var availableIds = string.Join(", ", systems.Select(s => s.Id));
                throw new ArgumentException($"System with ID {systemId} not found. Available system IDs: {availableIds}");
            }

            return system;
        }

        /// <summary>
        /// Lists all available bike share systems
        /// </summary>
        public static async Task ListAvailableSystemsAsync()
        {
            try
            {
                var systems = await LoadAllSystemsAsync();
                Console.WriteLine("Available Bike Share Systems:");
                Console.WriteLine("============================");
                foreach (var system in systems)
                {
                    var projectStatus = system.MaprouletteProjectId > 0 ? $"Project ID: {system.MaprouletteProjectId}" : "No Maproulette project";
                    Console.WriteLine($"ID: {system.Id} | {system.Name} ({system.City}) | {projectStatus}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading systems: {ex.Message}");
            }
        }
    }
}