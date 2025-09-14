using System.CommandLine;
using prepareBikeParking;

var root = new RootCommand("Bike Share Location Comparison Tool");

// 'run' command (default) - run full comparison for a system id
var systemIdArg = new Argument<int>("system-id", description: "Numeric ID of the bike share system (from bikeshare_systems.json)");
var runCommand = new Command("run", "Run comparison for a specific system ID") { systemIdArg };
runCommand.SetHandler(async (int systemId) =>
{
    await RunSystemFlow(systemId);
}, systemIdArg);

// Support root invocation with just the system-id (treat as run)
root.AddArgument(systemIdArg);
root.SetHandler(async (int systemId) =>
{
    await RunSystemFlow(systemId);
}, systemIdArg);

// list command
var listCommand = new Command("list", "List all available bike share systems");
listCommand.SetHandler(async () =>
{
    await BikeShareSystemLoader.ListAvailableSystemsAsync();
});

// test-project command
var projectIdArg = new Argument<int>("project-id", description: "Maproulette project ID to validate");
var testProjectCommand = new Command("test-project", "Test Maproulette project access") { projectIdArg };
testProjectCommand.SetHandler(async (int projectId) =>
{
    Console.WriteLine($"Testing Maproulette project access for project ID: {projectId}");
    Console.WriteLine("=".PadRight(60, '='));
    try
    {
        var isValid = await MaprouletteTaskCreator.ValidateProjectAsync(projectId);
        Console.WriteLine();
        Console.WriteLine("✅ Project validation successful!");
        Console.WriteLine("   You can use this project ID in your bikeshare_systems.json configuration.");
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("❌ Project validation failed!");
        Console.WriteLine($"   Error: {ex.Message}");
        Console.WriteLine("   Please fix the issues above before using this project ID.");
    }
}, projectIdArg);

// validate command
var validateSystemIdArg = new Argument<int>("system-id", description: "System ID to validate");
var validateCommand = new Command("validate", "Validate system setup with strict error checking") { validateSystemIdArg };
validateCommand.SetHandler(async (int validateSystemId) =>
{
    Console.WriteLine($"Validating system setup for system ID: {validateSystemId}");
    Console.WriteLine("=".PadRight(60, '='));
    try
    {
        var validateSystem = await BikeShareSystemLoader.LoadSystemByIdAsync(validateSystemId);
        Console.WriteLine($"✅ System configuration loaded: {validateSystem.Name} ({validateSystem.City})");
        SystemSetupHelper.ValidateSystemSetup(validateSystem.Name, throwOnMissing: true);
        Console.WriteLine("✅ System directory and files validated");
        SystemSetupHelper.ValidateInstructionFilesForTaskCreation(validateSystem.Name);
        Console.WriteLine("✅ Instruction files validated for task creation");
        if (validateSystem.MaprouletteProjectId > 0)
        {
            var projectValid = await MaprouletteTaskCreator.ValidateProjectAsync(validateSystem.MaprouletteProjectId);
            Console.WriteLine("✅ Maproulette project validated");
        }
        else
        {
            Console.WriteLine("ℹ️  No Maproulette project configured (task creation will be skipped)");
        }
        Console.WriteLine();
        Console.WriteLine("🎉 All validations passed! System is ready for use.");
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("❌ Validation failed!");
        Console.WriteLine($"   Error: {ex.Message}");
        Console.WriteLine("   Please fix the issues above before running the system.");
    }
}, validateSystemIdArg);

root.AddCommand(runCommand);
root.AddCommand(listCommand);
root.AddCommand(testProjectCommand);
root.AddCommand(validateCommand);

return await root.InvokeAsync(args);

async Task RunSystemFlow(int systemId)
{
    BikeShareSystem system;
    try
    {
        system = await BikeShareSystemLoader.LoadSystemByIdAsync(systemId);
    }
    catch (FileNotFoundException ex)
    {
        Console.WriteLine($"Configuration Error: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("To fix this:");
        Console.WriteLine("1. Ensure 'bikeshare_systems.json' exists in the project root directory");
        Console.WriteLine("2. Check that the file is properly formatted JSON");
        Console.WriteLine("3. See SETUP_NEW_SYSTEM.md for configuration examples");
        return;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading system configuration: {ex.Message}");
        Console.WriteLine("Use list command to see available systems or check SETUP_NEW_SYSTEM.md for help.");
        return;
    }

    Console.WriteLine($"Running bike share location comparison for {system.Name} ({system.City})");
    Console.WriteLine($"System ID: {system.Id}");
    Console.WriteLine($"Maproulette Project ID: {system.MaprouletteProjectId}");
    Console.WriteLine($"GBFS API: {system.GbfsApi}");
    Console.WriteLine($"Station Information URL: {system.GetStationInformationUrl()}");

    if (system.MaprouletteProjectId > 0)
    {
        Console.WriteLine($"Validating Maproulette project {system.MaprouletteProjectId}...");
        try
        {
            var projectValid = await MaprouletteTaskCreator.ValidateProjectAsync(system.MaprouletteProjectId);
            if (!projectValid)
            {
                throw new InvalidOperationException($"Maproulette project {system.MaprouletteProjectId} validation failed. Cannot proceed with task creation.");
            }
            Console.WriteLine("✅ Maproulette project validation successful.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Maproulette project validation failed: {ex.Message}");
            throw new InvalidOperationException($"Cannot proceed: Maproulette project {system.MaprouletteProjectId} validation failed. {ex.Message}", ex);
        }
    }
    else
    {
        Console.WriteLine("Info: No Maproulette project ID configured for this system. Task creation will be skipped.");
    }

    try
    {
        var validationResult = SystemSetupHelper.ValidateSystemSetup(system.Name, throwOnMissing: false);
        if (!validationResult.IsValid)
        {
            Console.WriteLine($"System Setup Issue: {validationResult.ErrorMessage}");
            Console.WriteLine("The tool will attempt to create missing files automatically...");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Critical system setup error: {ex.Message}");
        throw;
    }

    try
    {
        await RunBikeShareLocationComparison(system);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fatal Error: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("Troubleshooting tips:");
        Console.WriteLine("1. Check your internet connection and GBFS API URL");
        Console.WriteLine("2. Verify the system configuration in bikeshare_systems.json");
        Console.WriteLine("3. Ensure you have write permissions to the data_results directory");
        Console.WriteLine("4. See SETUP_NEW_SYSTEM.md for detailed setup instructions");
        Console.WriteLine();
        Console.WriteLine($"Full error details: {ex}");
    }
}

async Task RunBikeShareLocationComparison(BikeShareSystem system)
{
    // Ensure the system is properly set up with instruction files
    try
    {
        await SystemSetupHelper.EnsureSystemSetUpAsync(system.Name, system.Name, system.Name, system.City);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error setting up system files: {ex.Message}");
        Console.WriteLine("Please check directory permissions and try again.");
        throw;
    }

    // Check if this is a new system by looking for existing bikeshare.geojson file
    var geojsonFilePath = FileManager.GetSystemFullPath(system.Name, "bikeshare.geojson");
    var lastSyncDate = GitFunctions.GetLastCommitDateForFile(geojsonFilePath);
    bool isNewSystem = lastSyncDate == null;

    if (isNewSystem)
    {
        Console.WriteLine("No previous bikeshare.geojson file found in git history. This appears to be a new system setup.");
        Console.WriteLine("Using current date as the reference point for changes.");
        lastSyncDate = DateTime.Now;
    }
    else
    {
        Console.WriteLine($"Last sync date: {lastSyncDate}");
    }

    // Step 1: Get bike share locations data
    List<GeoPoint> locationsList;
    try
    {
        // Option A: Fetch new bike share locations from API (comment out if you want to use existing data)
        locationsList = await BikeShareDataFetcher.FetchFromApiAsync(system.GetStationInformationUrl());

        // Option B: Read bike share locations from existing file (uncomment to use instead of fetching)
        //locationsList = await BikeShareDataFetcher.ReadFromFileAsync(system.Name);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error fetching bike share data: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("Possible solutions:");
        Console.WriteLine($"1. Verify the GBFS API URL: {system.GetStationInformationUrl()}");
        Console.WriteLine("2. Check your internet connection");
        Console.WriteLine("3. Test the URL in a web browser to ensure it returns valid JSON");
        Console.WriteLine("4. Contact the bike share system administrator if the API is down");
        throw;
    }

    // Step 2: Generate and save the main geojson file
    await GeoJsonGenerator.GenerateMainFileAsync(locationsList, system.Name);

    // Step 3: Compare with last committed version and generate diff files
    await CompareAndGenerateDiffFiles(locationsList, system, isNewSystem);

    // NEW: Step 5: Compare with OSM data (uncomment to enable OSM comparison)
    await CompareWithOSMData(locationsList, system);

    Console.WriteLine("Do you want to create Maproulette tasks for the new locations? (y/N)");
    var confirm = Console.ReadKey().KeyChar;
    Console.WriteLine(); // New line after key press
    if (confirm.ToString().ToLower() != "y")
    {
        Console.WriteLine("Skipping Maproulette task creation.");
        return;
    }
    else
    {
        // Step 4: Create Maproulette task (comment out if you don't want to create tasks)
        if (system.MaprouletteProjectId > 0)
        {
            try
            {
                // Validate instruction files before creating tasks
                SystemSetupHelper.ValidateInstructionFilesForTaskCreation(system.Name);

                // Pass isNewSystem flag to avoid creating deletion tasks for new systems
                await MaprouletteTaskCreator.CreateTasksAsync(system.MaprouletteProjectId, lastSyncDate.Value, system.Name, isNewSystem);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("instruction files"))
            {
                Console.WriteLine($"❌ Cannot create Maproulette tasks: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("To fix this:");
                Console.WriteLine("1. Run the tool again to auto-generate missing instruction files");
                Console.WriteLine("2. Or manually create the instruction files in the system's instructions/ directory");
                Console.WriteLine("3. See SETUP_NEW_SYSTEM.md for instruction file templates");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating Maproulette tasks: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Possible solutions:");
                Console.WriteLine("1. Set MAPROULETTE_API_KEY environment variable");
                Console.WriteLine("2. Verify your Maproulette project ID is correct");
                Console.WriteLine("3. Check that you have permission to create tasks in the project");
                Console.WriteLine("4. Ensure instruction files exist in the instructions/ directory");
                throw;
            }
        }
        else
        {
            Console.WriteLine("Skipping Maproulette task creation: No valid project ID configured for this system.");
            Console.WriteLine("To enable Maproulette integration, add a valid project ID to bikeshare_systems.json");
        }
    }
}

async Task CompareAndGenerateDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system, bool isNewSystem = false)
{
    Console.WriteLine("Comparing with last committed version...");

    try
    {
        // Get the last committed version of the file
        var geojsonFile = FileManager.GetSystemFullPath(system.Name, "bikeshare.geojson");
        string lastCommittedVersion = GitDiffToGeojson.GetLastCommittedVersion(geojsonFile);

        // Parse the last committed version into a list of GeoPoints
        List<GeoPoint> lastCommittedPoints = lastCommittedVersion
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(GeoPoint.ParseLine)
            .ToList();

        // Compare the current points with the last committed points
        var (addedPoints, removedPoints, movedPoints, renamedPoints) =
            BikeShareComparer.ComparePoints(currentPoints, lastCommittedPoints, moveThreshold: 3);

        // Generate diff files
        await GeoJsonGenerator.GenerateDiffFilesAsync(addedPoints, removedPoints, movedPoints, renamedPoints, system.Name);

        Console.WriteLine($"Generated diff files: {addedPoints.Count} added, {removedPoints.Count} removed, {movedPoints.Count} moved, {renamedPoints.Count} renamed");
    }
    catch (FileNotFoundException ex) when (ex.Message.Contains("not found in git repository"))
    {
        Console.WriteLine("No previous version found in git repository. This appears to be a new system.");
        Console.WriteLine("Treating all current stations as newly added.");

        // For new systems, treat all current points as newly added
        await GenerateNewSystemDiffFiles(currentPoints, system);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Unable to compare with previous version: {ex.Message}");
        Console.WriteLine("This might be a new system or there might be an issue with git.");
        Console.WriteLine("Treating all current stations as newly added.");

        // For new systems or when git comparison fails, treat all current points as newly added
        await GenerateNewSystemDiffFiles(currentPoints, system);
    }
}

async Task GenerateNewSystemDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system)
{
    // For new systems, treat all current points as newly added
    var emptyList = new List<GeoPoint>();
    var emptyTupleList = new List<(GeoPoint current, GeoPoint old)>();

    // Generate diff files with all stations marked as added
    await GeoJsonGenerator.GenerateDiffFilesAsync(currentPoints, emptyList, emptyList, emptyTupleList, system.Name);

    Console.WriteLine($"Generated diff files for new system: {currentPoints.Count} stations marked as added, 0 removed, 0 moved, 0 renamed");
}

async Task CompareWithOSMData(List<GeoPoint> bikeshareApiPoints, BikeShareSystem system)
{
    try
    {
        Console.WriteLine($"Fetching current bikeshare stations from OpenStreetMap for {system.Name}...");

        // Ensure stations.overpass file exists for the system
        await OSMDataFetcher.EnsureStationsOverpassFileAsync(system.Name, system.City);

        // Fetch current OSM data using system-specific overpass query
        var osmPoints = await OSMDataFetcher.FetchFromOverpassApiAsync(system.Name);
        Console.WriteLine($"Found {osmPoints.Count} bikeshare stations in OSM");

        // Compare BikeShare API data with OSM data
        var (missingInOSM, extraInOSM, differentInOSM, renamedInOSM) =
            BikeShareComparer.ComparePoints(bikeshareApiPoints, osmPoints, moveThreshold: 30);

        // Generate comparison files
        await GeoJsonGenerator.GenerateOSMComparisonFilesAsync(missingInOSM, extraInOSM, differentInOSM, renamedInOSM, system.Name);

        await OsmFileFunctions.GenerateRenameOsmChangeFile(renamedInOSM, system.Name);

        Console.WriteLine($"OSM comparison: {missingInOSM.Count} missing in OSM, {extraInOSM.Count} extra in OSM, {differentInOSM.Count} moved in OSM, {renamedInOSM.Count} renamed in OSM");
        Console.WriteLine("Generated OSM comparison files: bikeshare_missing_in_osm.geojson, bikeshare_extra_in_osm.geojson, bikeshare_moved_in_osm.geojson, bikeshare_renamed_in_osm.geojson");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during OSM comparison: {ex.Message}");
        Console.WriteLine("OSM comparison failed, but the tool will continue with other operations.");
        Console.WriteLine("This might be due to network issues or problems with the Overpass API.");
    }
}
