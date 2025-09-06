// See https://aka.ms/new-console-template for more information
using prepareBikeParking;

// Parse command line arguments
if (args.Length == 0 || args.Any(arg => arg == "--help" || arg == "-h"))
{
    Console.WriteLine("Bike Share Location Comparison Tool");
    Console.WriteLine("===================================");
    Console.WriteLine();
    Console.WriteLine("Usage: prepareBikeParking <system-id>");
    Console.WriteLine("       prepareBikeParking --list");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  system-id    Numeric ID of the bike share system (from bikeshare_systems.json)");
    Console.WriteLine("  --list       List all available bike share systems");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  prepareBikeParking 1        # Run for Bike Share Toronto");
    Console.WriteLine("  prepareBikeParking 2        # Run for Bixi Montreal");
    Console.WriteLine("  prepareBikeParking --list   # Show all available systems");
    Console.WriteLine();
    Console.WriteLine("Note: The tool requires the MAPROULETTE_API_KEY environment variable to be set");
    Console.WriteLine("      for creating Maproulette tasks.");
    return;
}

// Handle --list command
if (args.Length == 1 && (args[0] == "--list" || args[0] == "-l"))
{
    await BikeShareSystemLoader.ListAvailableSystemsAsync();
    return;
}

if (args.Length != 1)
{
    Console.WriteLine("Error: Please provide exactly one system ID.");
    Console.WriteLine("Use --help for usage information or --list to see available systems.");
    return;
}

if (!int.TryParse(args[0], out int systemId))
{
    Console.WriteLine("Error: System ID must be a valid integer.");
    Console.WriteLine("Use --list to see available system IDs.");
    return;
}

// Load the bike share system configuration
BikeShareSystem system;
try
{
    system = await BikeShareSystemLoader.LoadSystemByIdAsync(systemId);
}
catch (Exception ex)
{
    Console.WriteLine($"Error loading system configuration: {ex.Message}");
    Console.WriteLine("Use --list to see available systems.");
    return;
}

Console.WriteLine($"Running bike share location comparison for {system.Name} ({system.City})");
Console.WriteLine($"System ID: {system.Id}");
Console.WriteLine($"Maproulette Project ID: {system.MaprouletteProjectId}");
Console.WriteLine($"GBFS API: {system.GbfsApi}");
Console.WriteLine($"Station Information URL: {system.GetStationInformationUrl()}");

// Validate Maproulette project ID if tasks will be created
if (system.MaprouletteProjectId <= 0)
{
    Console.WriteLine("Warning: No valid Maproulette project ID configured for this system. Task creation will be skipped.");
}

// Main execution flow - comment out any step you don't want to run
await RunBikeShareLocationComparison(system);

async Task RunBikeShareLocationComparison(BikeShareSystem system)
{
    // Ensure the system is properly set up with instruction files
    await SystemSetupHelper.EnsureSystemSetUpAsync(system.Name, system.Name, system.Name);
    
    // Check if this is a new system by looking for existing bikeshare.geojson file
    var geojsonFilePath = FileManager.GetSystemFullPath(system.Name, "bikeshare.geojson");
    var lastSyncDate = GitFunctions.GetLastCommitDateForFile(geojsonFilePath);
    
    if (lastSyncDate == null)
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
    // Option A: Fetch new bike share locations from API (comment out if you want to use existing data)
    var locationsList = await BikeShareDataFetcher.FetchFromApiAsync(system.GetStationInformationUrl());

    // Option B: Read bike share locations from existing file (uncomment to use instead of fetching)
    //var locationsList = await BikeShareDataFetcher.ReadFromFileAsync(system.Name);

    // Step 2: Generate and save the main geojson file
    await GeoJsonGenerator.GenerateMainFileAsync(locationsList, system.Name);

    // Step 3: Compare with last committed version and generate diff files
    await CompareAndGenerateDiffFiles(locationsList, system);

    // NEW: Step 5: Compare with OSM data (uncomment to enable OSM comparison)
    await CompareWithOSMData(locationsList, system);

    Console.WriteLine("Do you want to create Maproulette tasks for the new locations? (y/N)");
    var confirm = Console.ReadKey().KeyChar;
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
            await MaprouletteTaskCreator.CreateTasksAsync(system.MaprouletteProjectId, lastSyncDate.Value, system.Name);
        }
        else
        {
            Console.WriteLine("Skipping Maproulette task creation: No valid project ID configured for this system.");
        }
    }
}

async Task CompareAndGenerateDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system)
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

    Console.WriteLine($"Fetching current bikeshare stations from OpenStreetMap for {system.City}...");

    // Fetch current OSM data
    var osmPoints = await OSMDataFetcher.FetchFromOverpassApiAsync(system.City);
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
