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
    var lastSyncDate =
        GitFunctions.GetLastCommitDateForFile("../../../bikeshare.geojson") ??
        throw new Exception("Failed to retrieve last sync date. Ensure the file exists and is committed in the git repository.");
    Console.WriteLine($"Last sync date: {lastSyncDate}");

    // Step 1: Get bike share locations data
    // Option A: Fetch new bike share locations from API (comment out if you want to use existing data)
    var locationsList = await BikeShareDataFetcher.FetchFromApiAsync(system.GetStationInformationUrl());

    // Option B: Read bike share locations from existing file (uncomment to use instead of fetching)
    //var locationsList = await BikeShareDataFetcher.ReadFromFileAsync();

    // Step 2: Generate and save the main geojson file
    await GeoJsonGenerator.GenerateMainFileAsync(locationsList);

    // Step 3: Compare with last committed version and generate diff files
    await CompareAndGenerateDiffFiles(locationsList);

    // NEW: Step 5: Compare with OSM data (uncomment to enable OSM comparison)
    await CompareWithOSMData(locationsList, system.City);

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
            await MaprouletteTaskCreator.CreateTasksAsync(system.MaprouletteProjectId, lastSyncDate, system.Name);
        }
        else
        {
            Console.WriteLine("Skipping Maproulette task creation: No valid project ID configured for this system.");
        }
    }
}

async Task CompareAndGenerateDiffFiles(List<GeoPoint> currentPoints)
{
    Console.WriteLine("Comparing with last committed version...");

    // Get the last committed version of the file
    string lastCommittedVersion = GitDiffToGeojson.GetLastCommittedVersion();

    // Parse the last committed version into a list of GeoPoints
    List<GeoPoint> lastCommittedPoints = lastCommittedVersion
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(GeoPoint.ParseLine)
        .ToList();

    // Compare the current points with the last committed points
    var (addedPoints, removedPoints, movedPoints, renamedPoints) = 
        BikeShareComparer.ComparePoints(currentPoints, lastCommittedPoints, moveThreshold: 3);

    // Generate diff files
    await GeoJsonGenerator.GenerateDiffFilesAsync(addedPoints, removedPoints, movedPoints, renamedPoints);

    Console.WriteLine($"Generated diff files: {addedPoints.Count} added, {removedPoints.Count} removed, {movedPoints.Count} moved, {renamedPoints.Count} renamed");
}

async Task CompareWithOSMData(List<GeoPoint> bikeshareApiPoints, string cityName)
{

    Console.WriteLine($"Fetching current bikeshare stations from OpenStreetMap for {cityName}...");

    // Fetch current OSM data
    var osmPoints = await OSMDataFetcher.FetchFromOverpassApiAsync(cityName);
    Console.WriteLine($"Found {osmPoints.Count} bikeshare stations in OSM");

    // Compare BikeShare API data with OSM data
    var (missingInOSM, extraInOSM, differentInOSM, renamedInOSM) = 
        BikeShareComparer.ComparePoints(bikeshareApiPoints, osmPoints, moveThreshold: 30);

    // Generate comparison files
    await GeoJsonGenerator.GenerateOSMComparisonFilesAsync(missingInOSM, extraInOSM, differentInOSM, renamedInOSM);

    await OsmFileFunctions.GenerateRenameOsmChangeFile(renamedInOSM);

    Console.WriteLine($"OSM comparison: {missingInOSM.Count} missing in OSM, {extraInOSM.Count} extra in OSM, {differentInOSM.Count} moved in OSM, {renamedInOSM.Count} renamed in OSM");
    Console.WriteLine("Generated OSM comparison files: bikeshare_missing_in_osm.geojson, bikeshare_extra_in_osm.geojson, bikeshare_moved_in_osm.geojson, bikeshare_renamed_in_osm.geojson");

}
