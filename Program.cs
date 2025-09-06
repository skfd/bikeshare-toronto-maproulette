// See https://aka.ms/new-console-template for more information
using prepareBikeParking;

// Parse command line arguments
if (args.Length == 0 || args.Any(arg => arg == "--help" || arg == "-h"))
{
    Console.WriteLine("Bike Share Location Comparison Tool");
    Console.WriteLine("===================================");
    Console.WriteLine();
    Console.WriteLine("Usage: prepareBikeParking <city-name> <maproulette-project-id> [api-url]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  city-name              Name of the city (e.g., Toronto, Montreal, Vancouver)");
    Console.WriteLine("  maproulette-project-id Numeric ID of the Maproulette project");
    Console.WriteLine("  api-url               (Optional) Custom GBFS station_information API URL");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  prepareBikeParking Toronto 60735");
    Console.WriteLine("  prepareBikeParking Montreal 12345 https://montreal.example.com/gbfs/v1/en/station_information");
    Console.WriteLine();
    Console.WriteLine("Note: The tool requires the MAPROULETTE_API_KEY environment variable to be set");
    Console.WriteLine("      for creating Maproulette tasks.");
    return;
}

if (args.Length < 2)
{
    Console.WriteLine("Error: Missing required arguments.");
    Console.WriteLine("Use --help for usage information.");
    return;
}

string cityName = args[0];
if (!int.TryParse(args[1], out int maprouletteProjectId))
{
    Console.WriteLine("Error: Maproulette project ID must be a valid integer.");
    return;
}

string? apiUrl = args.Length > 2 ? args[2] : null;

Console.WriteLine($"Running bike share location comparison for {cityName}");
Console.WriteLine($"Maproulette Project ID: {maprouletteProjectId}");
if (!string.IsNullOrEmpty(apiUrl))
{
    Console.WriteLine($"Using custom API URL: {apiUrl}");
}

// Main execution flow - comment out any step you don't want to run
await RunBikeShareLocationComparison(cityName, maprouletteProjectId, apiUrl);

async Task RunBikeShareLocationComparison(string cityName, int maprouletteProjectId, string? apiUrl)
{
    var lastSyncDate =
        GitFunctions.GetLastCommitDateForFile("../../../bikeshare.geojson") ??
        throw new Exception("Failed to retrieve last sync date. Ensure the file exists and is committed in the git repository.");
    Console.WriteLine($"Last sync date: {lastSyncDate}");

    // Step 1: Get bike share locations data
    // Option A: Fetch new bike share locations from API (comment out if you want to use existing data)
    var locationsList = await BikeShareDataFetcher.FetchFromApiAsync(apiUrl);

    // Option B: Read bike share locations from existing file (uncomment to use instead of fetching)
    //var locationsList = await BikeShareDataFetcher.ReadFromFileAsync();

    // Step 2: Generate and save the main geojson file
    await GeoJsonGenerator.GenerateMainFileAsync(locationsList);

    // Step 3: Compare with last committed version and generate diff files
    await CompareAndGenerateDiffFiles(locationsList);

    // NEW: Step 5: Compare with OSM data (uncomment to enable OSM comparison)
    await CompareWithOSMData(locationsList, cityName);

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
        await MaprouletteTaskCreator.CreateTasksAsync(maprouletteProjectId, lastSyncDate, cityName);
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
