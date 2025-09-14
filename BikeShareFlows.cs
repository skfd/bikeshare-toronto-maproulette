using Serilog;
using prepareBikeParking;

public static class BikeShareFlows
{
    public static async Task RunSystemFlow(int systemId)
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

    public static async Task ValidateSystemAsync(int systemId)
    {
        Log.Information("Validating system setup {SystemId}", systemId);
        try
        {
            var system = await BikeShareSystemLoader.LoadSystemByIdAsync(systemId);
            Log.Information("System configuration loaded: {Name} ({City})", system.Name, system.City);
            SystemSetupHelper.ValidateSystemSetup(system.Name, throwOnMissing: true);
            Log.Information("System directory and files validated for {Name}", system.Name);
            SystemSetupHelper.ValidateInstructionFilesForTaskCreation(system.Name);
            Log.Information("Instruction files validated for {Name}", system.Name);
            if (system.MaprouletteProjectId > 0)
            {
                var projectValid = await MaprouletteTaskCreator.ValidateProjectAsync(system.MaprouletteProjectId);
                if (projectValid) Log.Information("Maproulette project {ProjectId} validated", system.MaprouletteProjectId);
            }
            else
            {
                Log.Warning("No Maproulette project configured for {Name} - task creation skipped", system.Name);
            }
            Log.Information("All validations passed for {Name}. System ready.", system.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Validation failed for system {SystemId}", systemId);
        }
    }

    public static async Task TestProjectAsync(int projectId)
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
            Log.Error(ex, "Project validation failed for {ProjectId}.", projectId);
        }
    }

    private static async Task RunBikeShareLocationComparison(BikeShareSystem system)
    {
        try
        {
            await SystemSetupHelper.EnsureSystemSetUpAsync(system.Name, system.Name, system.Name, system.City);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting up system files for {Name}.", system.Name);
            throw;
        }

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

        List<GeoPoint> locationsList;
        try
        {
            locationsList = await BikeShareDataFetcher.FetchFromApiAsync(system.GetStationInformationUrl());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed fetching bike share data for {Name}. URL={Url}", system.Name, system.GetStationInformationUrl());
            throw;
        }

        await GeoJsonGenerator.GenerateMainFileAsync(locationsList, system.Name);
        await CompareAndGenerateDiffFiles(locationsList, system, isNewSystem);
        await CompareWithOSMData(locationsList, system);

        Log.Information("Prompting user for Maproulette task creation (y/N)");
        Console.WriteLine("Do you want to create Maproulette tasks for the new locations? (y/N)");
        var confirm = Console.ReadKey().KeyChar;
        Console.WriteLine();
        if (confirm.ToString().ToLower() != "y")
        {
            Log.Information("User declined task creation.");
            return;
        }
        else
        {
            if (system.MaprouletteProjectId > 0)
            {
                try
                {
                    SystemSetupHelper.ValidateInstructionFilesForTaskCreation(system.Name);
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

    private static async Task CompareAndGenerateDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system, bool isNewSystem = false)
    {
        Log.Information("Comparing current data with last committed version for {Name}", system.Name);
        try
        {
            var geojsonFile = FileManager.GetSystemFullPath(system.Name, "bikeshare.geojson");
            string lastCommittedVersion = GitDiffToGeojson.GetLastCommittedVersion(geojsonFile);
            List<GeoPoint> lastCommittedPoints = lastCommittedVersion
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(GeoPoint.ParseLine)
                .ToList();

            var (addedPoints, removedPoints, movedPoints, renamedPoints) =
                BikeShareComparer.ComparePoints(currentPoints, lastCommittedPoints, moveThreshold: 3);

            await GeoJsonGenerator.GenerateDiffFilesAsync(addedPoints, removedPoints, movedPoints, renamedPoints, system.Name);
            Log.Information("Diff summary for {Name}: Added={Added} Removed={Removed} Moved={Moved} Renamed={Renamed}", system.Name, addedPoints.Count, removedPoints.Count, movedPoints.Count, renamedPoints.Count);
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("not found in git repository"))
        {
            Log.Warning("No previous version in git for {Name}; treating all stations as added.", system.Name);
            await GenerateNewSystemDiffFiles(currentPoints, system);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Git comparison failed for {Name}; treating all stations as added.", system.Name);
            await GenerateNewSystemDiffFiles(currentPoints, system);
        }
    }

    private static async Task GenerateNewSystemDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system)
    {
        var emptyList = new List<GeoPoint>();
        var emptyTupleList = new List<(GeoPoint current, GeoPoint old)>();
        await GeoJsonGenerator.GenerateDiffFilesAsync(currentPoints, emptyList, emptyList, emptyTupleList, system.Name);
        Log.Information("Generated diff files for new system {Name}: {Count} added", system.Name, currentPoints.Count);
    }

    private static async Task CompareWithOSMData(List<GeoPoint> bikeshareApiPoints, BikeShareSystem system)
    {
        try
        {
            Log.Information("Fetching OSM stations for {Name}", system.Name);
            await OSMDataFetcher.EnsureStationsOverpassFileAsync(system.Name, system.City);
            var osmPoints = await OSMDataFetcher.FetchFromOverpassApiAsync(system.Name);
            Log.Information("Fetched {Count} OSM stations for {Name}", osmPoints.Count, system.Name);
            var (missingInOSM, extraInOSM, differentInOSM, renamedInOSM) =
                BikeShareComparer.ComparePoints(bikeshareApiPoints, osmPoints, moveThreshold: 30);
            await GeoJsonGenerator.GenerateOSMComparisonFilesAsync(missingInOSM, extraInOSM, differentInOSM, renamedInOSM, system.Name);
            await OsmFileFunctions.GenerateRenameOsmChangeFile(renamedInOSM, system.Name);
            Log.Information("OSM comparison for {Name}: Missing={Missing} Extra={Extra} Moved={Moved} Renamed={Renamed}", system.Name, missingInOSM.Count, extraInOSM.Count, differentInOSM.Count, renamedInOSM.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSM comparison failed for {Name} - continuing", system.Name);
        }
    }
}
