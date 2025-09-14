using System.Text;

namespace prepareBikeParking
{
    /// <summary>
    /// Helper class for setting up new bike share systems
    /// </summary>
    public static class SystemSetupHelper
    {
        /// <summary>
        /// Creates the necessary directory structure and default instruction files for a new bike share system
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="operatorName">Name of the operator (for instruction templates)</param>
        /// <param name="brandName">Brand name (for instruction templates)</param>
        /// <param name="operatorType">Type of operator (public/private)</param>
        /// <param name="brandWikidataId">Wikidata ID for the brand (optional)</param>
        /// <param name="operatorWikidataId">Wikidata ID for the operator (optional)</param>
        /// <param name="cityName">City name for Overpass query (optional, defaults to system name)</param>
        public static async Task SetupNewSystemAsync(
            string systemName,
            string operatorName,
            string brandName,
            string operatorType = "public",
            string? brandWikidataId = null,
            string? operatorWikidataId = null,
            string? cityName = null)
        {
            Serilog.Log.Information("Setting up new system: {System}", systemName);

            // Create the system directory structure
            var systemDir = FileManager.GetSystemFullPath(systemName, "");
            var instructionsDir = FileManager.GetSystemFullPath(systemName, "instructions");

            if (!Directory.Exists(systemDir))
            {
                Directory.CreateDirectory(systemDir);
                Serilog.Log.Information("Created system directory {Dir}", systemDir);
            }

            if (!Directory.Exists(instructionsDir))
            {
                Directory.CreateDirectory(instructionsDir);
                Serilog.Log.Information("Created instructions directory {Dir}", instructionsDir);
            }

            // Create instruction files
            await CreateInstructionFileAsync(systemName, "added.md", GenerateAddedInstructions(operatorName, brandName, operatorType, brandWikidataId, operatorWikidataId));
            await CreateInstructionFileAsync(systemName, "removed.md", GenerateRemovedInstructions());
            await CreateInstructionFileAsync(systemName, "moved.md", GenerateMovedInstructions());
            await CreateInstructionFileAsync(systemName, "renamed.md", GenerateRenamedInstructions());

            // Create stations.overpass file for OSM data fetching
            await OSMDataFetcher.EnsureStationsOverpassFileAsync(systemName, cityName ?? systemName);

            Serilog.Log.Information("Successfully set up new system {System}. Directory: {Dir}", systemName, systemDir);
            Serilog.Log.Information("Run tool with system ID to fetch and process data.");
        }

        /// <summary>
        /// Creates an instruction file for a specific task type
        /// </summary>
        private static async Task CreateInstructionFileAsync(string systemName, string fileName, string content)
        {
            var filePath = Path.Combine("instructions", fileName);

            if (!FileManager.SystemFileExists(systemName, filePath))
            {
                await FileManager.WriteSystemTextFileAsync(systemName, filePath, content);
                Serilog.Log.Debug("Created instruction file {File}", fileName);
            }
            else
            {
                Serilog.Log.Debug("Instruction file already exists {File}", fileName);
            }
        }

        /// <summary>
        /// Generates the "added" instruction template
        /// </summary>
        private static string GenerateAddedInstructions(string operatorName, string brandName, string operatorType, string? brandWikidataId, string? operatorWikidataId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Add a point with these tags, or update existing point with them:");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("ref={{address}}");
            sb.AppendLine("name={{name}}");
            sb.AppendLine("capacity={{capacity}}");
            sb.AppendLine("fixme=please set exact location");
            sb.AppendLine("amenity=bicycle_rental");
            sb.AppendLine("bicycle_rental=docking_station");
            sb.AppendLine($"brand={brandName}");

            if (!string.IsNullOrEmpty(brandWikidataId))
            {
                sb.AppendLine($"brand:wikidata={brandWikidataId}");
            }

            if (brandName != operatorName)
            {
                sb.AppendLine($"network={brandName}");
                if (!string.IsNullOrEmpty(brandWikidataId))
                {
                    sb.AppendLine($"network:wikidata={brandWikidataId}");
                }
            }

            sb.AppendLine($"operator={operatorName}");
            sb.AppendLine($"operator:type={operatorType}");

            if (!string.IsNullOrEmpty(operatorWikidataId))
            {
                sb.AppendLine($"operator:wikidata={operatorWikidataId}");
            }

            sb.AppendLine("```");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Generates the "removed" instruction template
        /// </summary>
        private static string GenerateRemovedInstructions()
        {
            return @"This station has been removed from the official bike share API. Please verify if it still exists in reality and remove it from OpenStreetMap if confirmed.

Steps:
1. Check if the station still exists physically at the mapped location
2. If the station is indeed gone, delete the point from OpenStreetMap
3. If the station still exists but is temporarily unavailable, add a note about its status
4. If you're unsure, add a note requesting verification from local mappers

";
        }

        /// <summary>
        /// Generates the "moved" instruction template
        /// </summary>
        private static string GenerateMovedInstructions()
        {
            return @"This station has moved to a new location according to the official bike share API. Please verify the new location and update the point accordingly.

Steps:
1. Verify the new coordinates are correct
2. Move the existing point to the new location
3. Update any other tags if necessary (name, capacity, etc.)
4. If you find the station at a different location than suggested, use the actual observed location

";
        }

        /// <summary>
        /// Generates the "renamed" instruction template
        /// </summary>
        private static string GenerateRenamedInstructions()
        {
            return @"This station has been renamed according to the official bike share API. Please verify the new name and update the point accordingly.

Steps:
1. Verify the new name is correct
2. Update the 'name' tag with the new official name
3. Keep other tags unchanged unless they also need updating
4. If you observe a different name on-site, prioritize the name actually displayed at the station

";
        }

        /// <summary>
        /// Checks if a system has been properly set up with required files and directories
        /// </summary>
        /// <param name="systemName">Name of the bike share system to check</param>
        /// <returns>True if the system is properly set up, false otherwise</returns>
        public static bool IsSystemSetUp(string systemName)
        {
            var requiredFiles = new[]
            {
                Path.Combine("instructions", "added.md"),
                Path.Combine("instructions", "removed.md"),
                Path.Combine("instructions", "moved.md"),
                Path.Combine("instructions", "renamed.md")
            };

            return requiredFiles.All(file => FileManager.SystemFileExists(systemName, file));
        }

        /// <summary>
        /// Ensures a system is properly set up, creating missing files with default templates if needed
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="operatorName">Operator name (used for default templates)</param>
        /// <param name="brandName">Brand name (used for default templates)</param>
        /// <param name="cityName">City name for Overpass query (optional)</param>
        public static async Task EnsureSystemSetUpAsync(string systemName, string operatorName, string brandName, string? cityName = null)
        {
            if (!IsSystemSetUp(systemName))
            {
                Serilog.Log.Warning("System {System} not fully set up. Creating missing instruction files...", systemName);
                await SetupNewSystemAsync(systemName, operatorName, brandName, "public", null, null, cityName);
            }
            else
            {
                // Even if system is set up, ensure stations.overpass file exists
                await OSMDataFetcher.EnsureStationsOverpassFileAsync(systemName, cityName ?? systemName);
            }
        }

        /// <summary>
        /// Validates system configuration and throws errors for critical missing components
        /// </summary>
        /// <param name="systemName">Name of the bike share system to validate</param>
        /// <param name="throwOnMissing">If true, throws exceptions for missing critical components</param>
        /// <returns>Validation result with detailed error information</returns>
        public static SystemValidationResult ValidateSystemSetup(string systemName, bool throwOnMissing = false)
        {
            var result = new SystemValidationResult { SystemName = systemName, IsValid = true };
            var missingFiles = new List<string>();

            var requiredFiles = new[]
            {
                Path.Combine("instructions", "added.md"),
                Path.Combine("instructions", "removed.md"),
                Path.Combine("instructions", "moved.md"),
                Path.Combine("instructions", "renamed.md")
            };

            foreach (var file in requiredFiles)
            {
                if (!FileManager.SystemFileExists(systemName, file))
                {
                    missingFiles.Add(file);
                }
            }

            if (missingFiles.Any())
            {
                result.IsValid = false;
                result.MissingFiles = missingFiles;
                result.ErrorMessage = $"System '{systemName}' is missing required files: {string.Join(", ", missingFiles)}";

                if (throwOnMissing)
                {
                    throw new InvalidOperationException($"Critical system setup error for '{systemName}': Missing required instruction files: {string.Join(", ", missingFiles)}. These files are required for Maproulette task creation.");
                }
            }

            // Check if system directory exists
            var systemDir = FileManager.GetSystemFullPath(systemName, "");
            if (!Directory.Exists(systemDir))
            {
                result.IsValid = false;
                result.ErrorMessage = $"System directory does not exist: {systemDir}";

                if (throwOnMissing)
                {
                    throw new DirectoryNotFoundException($"Critical system setup error for '{systemName}': System directory does not exist: {systemDir}. Run system setup first.");
                }
            }

            return result;
        }

        /// <summary>
        /// Validates that all required instruction files exist for Maproulette task creation
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <exception cref="InvalidOperationException">Thrown when critical instruction files are missing</exception>
        public static void ValidateInstructionFilesForTaskCreation(string systemName)
        {
            var requiredFiles = new[]
            {
                Path.Combine("instructions", "added.md"),
                Path.Combine("instructions", "removed.md"),
                Path.Combine("instructions", "moved.md")
            };

            var missingFiles = new List<string>();

            foreach (var file in requiredFiles)
            {
                if (!FileManager.SystemFileExists(systemName, file))
                {
                    missingFiles.Add(file);
                }
                else
                {
                    // Check if file has content
                    try
                    {
                        var content = FileManager.ReadSystemTextFileAsync(systemName, file).Result;
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            missingFiles.Add($"{file} (empty)");
                        }
                    }
                    catch
                    {
                        missingFiles.Add($"{file} (unreadable)");
                    }
                }
            }

            if (missingFiles.Any())
            {
                throw new InvalidOperationException($"Cannot create Maproulette tasks for system '{systemName}': Missing or invalid instruction files: {string.Join(", ", missingFiles)}. " +
                    $"These files are required for task creation. Run the tool to auto-generate them or create them manually.");
            }
        }
    }

    /// <summary>
    /// Result of system validation with detailed error information
    /// </summary>
    public class SystemValidationResult
    {
        public string SystemName { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> MissingFiles { get; set; } = new List<string>();
    }
}