using System.Text.Json;
using Serilog;

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
                throw new FileNotFoundException($"Configuration file 'bikeshare_systems.json' not found at path: {Path.GetFullPath(ConfigFilePath)}\n\n" +
                    "To fix this:\n" +
                    "1. Ensure 'bikeshare_systems.json' exists in the project root directory\n" +
                    "2. Use the example configuration from SETUP_NEW_SYSTEM.md\n" +
                    "3. Verify the file has proper JSON formatting");
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(ConfigFilePath);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    throw new InvalidOperationException($"Configuration file 'bikeshare_systems.json' is empty. Please add at least one bike share system configuration.");
                }

                var result = JsonSerializer.Deserialize<List<BikeShareSystem>>(jsonContent);

                if (result == null)
                {
                    throw new InvalidOperationException($"Failed to parse configuration file 'bikeshare_systems.json'. The file may contain invalid JSON.\n\n" +
                        "Common issues:\n" +
                        "1. Missing commas between objects\n" +
                        "2. Trailing commas after last object\n" +
                        "3. Incorrect quotation marks\n" +
                        "4. Missing brackets [ ]\n\n" +
                        "See SETUP_NEW_SYSTEM.md for valid examples.");
                }

                if (result.Count == 0)
                {
                    throw new InvalidOperationException("Configuration file 'bikeshare_systems.json' contains no systems. Please add at least one bike share system.");
                }

                // Validate each system configuration
                ValidateSystemConfigurations(result);

                return result;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"JSON parsing error in 'bikeshare_systems.json': {ex.Message}\n\n" +
                    "Please check the file format and ensure it contains valid JSON. See SETUP_NEW_SYSTEM.md for examples.", ex);
            }
            catch (Exception ex) when (ex is not FileNotFoundException and not InvalidOperationException)
            {
                throw new InvalidOperationException($"Unexpected error loading 'bikeshare_systems.json': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates system configurations and provides helpful error messages
        /// </summary>
        private static void ValidateSystemConfigurations(List<BikeShareSystem> systems)
        {
            var errors = new List<string>();
            var usedIds = new HashSet<int>();

            foreach (var system in systems)
            {
                var systemErrors = new List<string>();

                // Check for duplicate IDs
                if (usedIds.Contains(system.Id))
                {
                    systemErrors.Add($"Duplicate ID {system.Id}");
                }
                usedIds.Add(system.Id);

                // Validate required fields
                if (string.IsNullOrWhiteSpace(system.Name))
                    systemErrors.Add("Name is required");

                if (string.IsNullOrWhiteSpace(system.City))
                    systemErrors.Add("City is required");

                if (string.IsNullOrWhiteSpace(system.GbfsApi))
                    systemErrors.Add("GBFS API URL is required");
                else if (!Uri.TryCreate(system.GbfsApi, UriKind.Absolute, out var uri) ||
                         (uri.Scheme != "http" && uri.Scheme != "https"))
                    systemErrors.Add("GBFS API must be a valid HTTP/HTTPS URL");

                if (systemErrors.Any())
                {
                    errors.Add($"System ID {system.Id} ('{system.Name}'): {string.Join(", ", systemErrors)}");
                }
            }

            if (errors.Any())
            {
                throw new InvalidOperationException($"Configuration validation errors:\n{string.Join("\n", errors)}\n\n" +
                    "Please fix these issues in bikeshare_systems.json. See SETUP_NEW_SYSTEM.md for help.");
            }
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
                var availableSystems = string.Join("\n", systems.Select(s => $"  ID {s.Id}: {s.Name} ({s.City})"));

                throw new ArgumentException($"System with ID {systemId} not found.\n\n" +
                    $"Available systems:\n{availableSystems}\n\n" +
                    $"Use one of these IDs: {availableIds}\n" +
                    "Or add a new system to bikeshare_systems.json (see SETUP_NEW_SYSTEM.md)");
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
                Log.Information("Available Bike Share Systems (Count={Count})", systems.Count);

                if (systems.Count == 0)
                {
                    Log.Warning("No bike share systems configured. Add entries to bikeshare_systems.json. See SETUP_NEW_SYSTEM.md");
                    return;
                }

                foreach (var system in systems)
                {
                    var projectStatus = system.MaprouletteProjectId > 0 ? system.MaprouletteProjectId.ToString() : "None";
                    var apiValid = Uri.TryCreate(system.GbfsApi, UriKind.Absolute, out _);
                    Log.Information("System {Id}: {Name} ({City}) | Project={ProjectStatus} | ApiValid={ApiValid} | Url={Url}",
                        system.Id, system.Name, system.City, projectStatus, apiValid, system.GbfsApi);
                }
                Log.Information("Total systems configured: {Count}", systems.Count);
                Log.Information("Usage: dotnet run -- run --system-id <id>");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading bike share systems configuration");
                Log.Information("To fix: ensure bikeshare_systems.json exists, JSON is valid, and matches SETUP_NEW_SYSTEM.md");
            }
        }
    }
}
