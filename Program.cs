// See https://aka.ms/new-console-template for more information
using prepareBikeParking;

// Main execution flow - comment out any step you don't want to run
await RunBikeShareLocationComparison();

async Task RunBikeShareLocationComparison()
{
    var lastSyncDate =
        GitFunctions.GetLastCommitDateForFile("../../../bikeshare.geojson") ??
        throw new Exception("Failed to retrieve last sync date. Ensure the file exists and is committed in the git repository.");
    Console.WriteLine($"Last sync date: {lastSyncDate}");

    // Step 1: Get bike share locations data
    // Option A: Fetch new bike share locations from API (comment out if you want to use existing data)
    var locationsList = await BikeShareDataFetcher.FetchFromApiAsync();

    // Option B: Read bike share locations from existing file (uncomment to use instead of fetching)
    //var locationsList = await BikeShareDataFetcher.ReadFromFileAsync();

    // Step 2: Generate and save the main geojson file
    await GeoJsonGenerator.GenerateMainFileAsync(locationsList);

    // Step 3: Compare with last committed version and generate diff files
    await CompareAndGenerateDiffFiles(locationsList);

    // NEW: Step 5: Compare with OSM data (uncomment to enable OSM comparison)
    await CompareWithOSMData(locationsList);

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
        await MaprouletteTaskCreator.CreateTasksAsync(60735, lastSyncDate);
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
    var (addedPoints, removedPoints, movedPoints, renamedPoints) = BikeShareComparer.ComparePoints(currentPoints, lastCommittedPoints);

    // Generate diff files
    await GeoJsonGenerator.GenerateDiffFilesAsync(addedPoints, removedPoints, movedPoints, renamedPoints);

    Console.WriteLine($"Generated diff files: {addedPoints.Count} added, {removedPoints.Count} removed, {movedPoints.Count} moved, {renamedPoints.Count} renamed");
}

async Task CompareWithOSMData(List<GeoPoint> bikeshareApiPoints)
{

    Console.WriteLine("Fetching current bikeshare stations from OpenStreetMap...");

    // Fetch current OSM data
    var osmPoints = await OSMDataFetcher.FetchFromOverpassApiAsync();
    Console.WriteLine($"Found {osmPoints.Count} bikeshare stations in OSM");

    // Compare BikeShare API data with OSM data
    var (missingInOSM, extraInOSM, differentInOSM, renamedInOSM) = BikeShareComparer.ComparePoints(bikeshareApiPoints, osmPoints);

    // Generate comparison files
    await GeoJsonGenerator.GenerateOSMComparisonFilesAsync(missingInOSM, extraInOSM, differentInOSM, renamedInOSM);

    Console.WriteLine($"OSM comparison: {missingInOSM.Count} missing in OSM, {extraInOSM.Count} extra in OSM, {differentInOSM.Count} moved in OSM, {renamedInOSM.Count} renamed in OSM");
    Console.WriteLine("Generated OSM comparison files: bikeshare_missing_in_osm.geojson, bikeshare_extra_in_osm.geojson, bikeshare_moved_in_osm.geojson, bikeshare_renamed_in_osm.geojson");

}
