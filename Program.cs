using System.CommandLine;
using prepareBikeParking;
using Serilog;
using Serilog.Events;

// Configure Serilog (console + rolling file)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate: "[${Timestamp:HH:mm:ss} ${Level:u3}] ${Message:lj}${NewLine}${Exception}")
    .WriteTo.File("logs/bikeshare-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "${Timestamp:O} [${Level:u3}] ${Message:lj} ${Properties:j}${NewLine}${Exception}")
    .CreateLogger();

try
{
    Log.Information("Startup arguments: {Args}", args);

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
    Log.Information("Validating Maproulette project {ProjectId}", projectId);
    try
    {
        var isValid = await MaprouletteTaskCreator.ValidateProjectAsync(projectId);
        if (isValid)
        {
            Log.Information("Project {ProjectId} validation succeeded. ID can be used in configuration.", projectId);
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Project validation failed for {ProjectId}. Fix issues before using this ID.", projectId);
    }
}, projectIdArg);

// validate command
var validateSystemIdArg = new Argument<int>("system-id", description: "System ID to validate");
var validateCommand = new Command("validate", "Validate system setup with strict error checking") { validateSystemIdArg };
validateCommand.SetHandler(async (int validateSystemId) =>
{
    Log.Information("Validating system setup {SystemId}", validateSystemId);
    try
    {
        var validateSystem = await BikeShareSystemLoader.LoadSystemByIdAsync(validateSystemId);
        Log.Information("System configuration loaded: {Name} ({City})", validateSystem.Name, validateSystem.City);
        SystemSetupHelper.ValidateSystemSetup(validateSystem.Name, throwOnMissing: true);
        Log.Information("System directory and files validated for {Name}", validateSystem.Name);
        SystemSetupHelper.ValidateInstructionFilesForTaskCreation(validateSystem.Name);
        Log.Information("Instruction files validated for task creation for {Name}", validateSystem.Name);
        if (validateSystem.MaprouletteProjectId > 0)
        {
            var projectValid = await MaprouletteTaskCreator.ValidateProjectAsync(validateSystem.MaprouletteProjectId);
            if (projectValid) Log.Information("Maproulette project {ProjectId} validated", validateSystem.MaprouletteProjectId);
        }
        else
        {
            Log.Warning("No Maproulette project configured for {Name} - task creation skipped", validateSystem.Name);
        }
        Log.Information("All validations passed for {Name}. System ready.", validateSystem.Name);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Validation failed for system {SystemId}", validateSystemId);
    }
}, validateSystemIdArg);

root.AddCommand(runCommand);
root.AddCommand(listCommand);
root.AddCommand(testProjectCommand);
root.AddCommand(validateCommand);

    var exitCode = await root.InvokeAsync(args);
    Log.Information("Exiting with code {Code}", exitCode);
    return exitCode;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception caused termination");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

async Task RunSystemFlow(int systemId)
{
    BikeShareSystem system;
    try
    {
        system = await BikeShareSystemLoader.LoadSystemByIdAsync(systemId);
    }
    catch (FileNotFoundException ex)
    {
        Log.Error(ex, "Configuration file missing or invalid. See setup guide.");
        return;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed loading system configuration for {SystemId}", systemId);
        return;
    }

    Log.Information("Starting comparison run for {Name} ({City}) Id={Id} Project={ProjectId}", system.Name, system.City, system.Id, system.MaprouletteProjectId);
    Log.Debug("System endpoints: GbfsApi={Api} StationInfoUrl={StationUrl}", system.GbfsApi, system.GetStationInformationUrl());

    if (system.MaprouletteProjectId > 0)
    {
        Log.Information("Validating Maproulette project {ProjectId}", system.MaprouletteProjectId);
        try
        {
            var projectValid = await MaprouletteTaskCreator.ValidateProjectAsync(system.MaprouletteProjectId);
            if (!projectValid)
            {
                throw new InvalidOperationException($"Maproulette project {system.MaprouletteProjectId} validation failed. Cannot proceed with task creation.");
            }
            Log.Information("Maproulette project {ProjectId} validation successful", system.MaprouletteProjectId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Maproulette project validation failed for {ProjectId}", system.MaprouletteProjectId);
            throw new InvalidOperationException($"Cannot proceed: Maproulette project {system.MaprouletteProjectId} validation failed. {ex.Message}", ex);
        }
    }
    else
    {
        Log.Warning("No Maproulette project configured for {Name}. Task creation skipped.", system.Name);
    }

    try
    {
        var validationResult = SystemSetupHelper.ValidateSystemSetup(system.Name, throwOnMissing: false);
        if (!validationResult.IsValid)
        {
            Log.Warning("System setup issue for {Name}: {Issue}. Attempting auto-create.", system.Name, validationResult.ErrorMessage);
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Critical system setup error for {Name}", system.Name);
        throw;
    }

    try
    {
        await RunBikeShareLocationComparison(system);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Fatal error during run for {Name}", system.Name);
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
        Log.Error(ex, "Error setting up system files for {Name}. Check directory permissions.", system.Name);
        throw;
    }

    // Check if this is a new system by looking for existing bikeshare.geojson file
    var geojsonFilePath = FileManager.GetSystemFullPath(system.Name, "bikeshare.geojson");
    var lastSyncDate = GitFunctions.GetLastCommitDateForFile(geojsonFilePath);
    bool isNewSystem = lastSyncDate == null;

    if (isNewSystem)
    {
        Log.Information("No previous bikeshare.geojson found for {Name}. Treating as new system.", system.Name);
        lastSyncDate = DateTime.Now;
    }
    else
    {
        Log.Information("Last sync date for {Name}: {LastSync}", system.Name, lastSyncDate);
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
        Log.Error(ex, "Failed fetching bike share data for {Name}. URL={Url}", system.Name, system.GetStationInformationUrl());
        throw;
    }

    // Step 2: Generate and save the main geojson file
    await GeoJsonGenerator.GenerateMainFileAsync(locationsList, system.Name);

    // Step 3: Compare with last committed version and generate diff files
    await CompareAndGenerateDiffFiles(locationsList, system, isNewSystem);

    // NEW: Step 5: Compare with OSM data (uncomment to enable OSM comparison)
    await CompareWithOSMData(locationsList, system);

    Log.Information("Prompting user for Maproulette task creation (y/N)");
    var confirm = Console.ReadKey().KeyChar;
    Console.WriteLine();
    if (confirm.ToString().ToLower() != "y")
    {
        Log.Information("User declined task creation.");
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
                Log.Error(ex, "Instruction file issue prevented task creation for {Name}", system.Name);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating Maproulette tasks for {Name}", system.Name);
                throw;
            }
        }
        else
        {
            Log.Warning("No valid Maproulette project ID configured for {Name}; skipping task creation.", system.Name);
        }
    }
}

async Task CompareAndGenerateDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system, bool isNewSystem = false)
{
    Log.Information("Comparing current data with last committed version for {Name}", system.Name);

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

        Log.Information("Diff summary for {Name}: Added={Added} Removed={Removed} Moved={Moved} Renamed={Renamed}", system.Name, addedPoints.Count, removedPoints.Count, movedPoints.Count, renamedPoints.Count);
    }
    catch (FileNotFoundException ex) when (ex.Message.Contains("not found in git repository"))
    {
        Log.Warning("No previous version in git for {Name}; treating all stations as added.", system.Name);

        // For new systems, treat all current points as newly added
        await GenerateNewSystemDiffFiles(currentPoints, system);
    }
    catch (Exception ex)
    {
    Log.Error(ex, "Git comparison failed for {Name}; treating all stations as added.", system.Name);

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

    Log.Information("Generated diff files for new system {Name}: {Count} added", system.Name, currentPoints.Count);
}

async Task CompareWithOSMData(List<GeoPoint> bikeshareApiPoints, BikeShareSystem system)
{
    try
    {
    Log.Information("Fetching OSM stations for {Name}", system.Name);

        // Ensure stations.overpass file exists for the system
        await OSMDataFetcher.EnsureStationsOverpassFileAsync(system.Name, system.City);

        // Fetch current OSM data using system-specific overpass query
        var osmPoints = await OSMDataFetcher.FetchFromOverpassApiAsync(system.Name);
    Log.Information("Fetched {Count} OSM stations for {Name}", osmPoints.Count, system.Name);

        // Compare BikeShare API data with OSM data
        var (missingInOSM, extraInOSM, differentInOSM, renamedInOSM) =
            BikeShareComparer.ComparePoints(bikeshareApiPoints, osmPoints, moveThreshold: 30);

        // Generate comparison files
        await GeoJsonGenerator.GenerateOSMComparisonFilesAsync(missingInOSM, extraInOSM, differentInOSM, renamedInOSM, system.Name);

        await OsmFileFunctions.GenerateRenameOsmChangeFile(renamedInOSM, system.Name);

        Log.Information("OSM comparison for {Name}: Missing={Missing} Extra={Extra} Moved={Moved} Renamed={Renamed}", system.Name, missingInOSM.Count, extraInOSM.Count, differentInOSM.Count, renamedInOSM.Count);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "OSM comparison failed for {Name} - continuing", system.Name);
    }
}
