// See https://aka.ms/new-console-template for more information
using AngleSharp.Html.Parser;
using prepareBikeParking;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

// Main execution flow - comment out any step you don't want to run
await RunBikeShareLocationComparison();

async Task RunBikeShareLocationComparison()
{
    // Step 1: Get bike share locations data
    // Option A: Fetch new bike share locations from API (comment out if you want to use existing data)
    //var locationsList = await BikeShareDataFetcher.FetchFromApiAsync();
    
    // Option B: Read bike share locations from existing file (uncomment to use instead of fetching)
     var locationsList = await BikeShareDataFetcher.ReadFromFileAsync();
    
    // Step 2: Generate and save the main geojson file
    await GeoJsonGenerator.GenerateMainFileAsync(locationsList);
    
    // Step 3: Compare with last committed version and generate diff files
    await CompareAndGenerateDiffFiles(locationsList);
    
    // Step 4: Create Maproulette task (comment out if you don't want to create tasks)
     await MaprouletteTaskCreator.CreateTasksAsync(53785);
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
