using Serilog;
using prepareBikeParking.Services;
using prepareBikeParking.ServicesImpl;
using prepareBikeParking;
using prepareBikeParking.Logging;
using System.Diagnostics;

public class BikeShareFlows
{
    private readonly IBikeShareDataFetcher _bikeShareFetcher;
    private readonly IOSMDataFetcher _osmFetcher;
    private readonly IGeoJsonWriter _geoWriter;
    private readonly IComparerService _comparer;
    private readonly IGitReader _git;
    private readonly IMaprouletteService _maproulette;
    private readonly ISystemSetupService _systemSetup;
    private readonly IFilePathProvider _paths;
    private readonly IPromptService _prompt;
    private readonly IBikeShareSystemLoader _systemLoader;
    private readonly IOsmChangeWriter _osmChangeWriter;

    public BikeShareFlows(
        IBikeShareDataFetcher bikeShareFetcher,
        IOSMDataFetcher osmFetcher,
        IGeoJsonWriter geoWriter,
        IComparerService comparer,
        IGitReader git,
        IMaprouletteService maproulette,
        ISystemSetupService systemSetup,
        IFilePathProvider paths,
        IPromptService prompt,
        IBikeShareSystemLoader systemLoader,
        IOsmChangeWriter osmChangeWriter)
    {
        _bikeShareFetcher = bikeShareFetcher;
        _osmFetcher = osmFetcher;
        _geoWriter = geoWriter;
        _comparer = comparer;
        _git = git;
        _maproulette = maproulette;
        _systemSetup = systemSetup;
        _paths = paths;
        _prompt = prompt;
        _systemLoader = systemLoader;
        _osmChangeWriter = osmChangeWriter;
    }

    public async Task RunSystemFlow(int systemId)
    {
        using var operationTimer = Log.Logger.TimedOperation($"RunSystemFlow-{systemId}");

        BikeShareSystem system;
        try
        {
            system = await _systemLoader.LoadByIdAsync(systemId);
        }
        catch (FileNotFoundException ex)
        {
            Log.Error(ex, "Configuration file missing or invalid. SystemId: {SystemId}", systemId);
            ConsoleUI.PrintError($"Configuration file missing or invalid (system id {systemId}): {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed loading system configuration. SystemId: {SystemId}", systemId);
            ConsoleUI.PrintError($"Failed loading system configuration for id {systemId}: {ex.Message}");
            return;
        }

        ConsoleUI.PrintHeader($"Sync: {system.Name} ({system.City})");

        var systemLogger = Log.Logger.ForBikeShareSystem(system.Name, system.Id);
        systemLogger.Information("Starting comparison run. City: {City}, MaprouletteProject: {ProjectId}",
            system.City, system.MaprouletteProjectId);
        systemLogger.Debug("System endpoints configured. GbfsApi: {Api}, StationInfoUrl: {StationUrl}",
            system.GbfsApi, system.GetStationInformationUrl());

        var projectValidForTasks = false;
        if (system.MaprouletteProjectId > 0)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY")))
            {
                systemLogger.Error("MAPROULETTE_API_KEY environment variable is not set. Aborting.");
                ConsoleUI.PrintError("MAPROULETTE_API_KEY environment variable is not set.");
                ConsoleUI.PrintAction("Set MAPROULETTE_API_KEY before running, or set MaprouletteProjectId to 0 in bikeshare_systems.json to skip task creation.");
                return;
            }

            ConsoleUI.PrintStep($"Validating MapRoulette project {system.MaprouletteProjectId}");
            systemLogger.Information("Validating Maproulette project. ProjectId: {ProjectId}", system.MaprouletteProjectId);
            try
            {
                var projectValid = await _maproulette.ValidateProjectAsync(system.MaprouletteProjectId);
                if (!projectValid)
                {
                    systemLogger.Error("Maproulette project validation failed. ProjectId: {ProjectId}. Aborting.",
                        system.MaprouletteProjectId);
                    ConsoleUI.PrintError($"MapRoulette project {system.MaprouletteProjectId} validation failed. Aborting.");
                    return;
                }
                systemLogger.Information("Maproulette project validated successfully. ProjectId: {ProjectId}",
                    system.MaprouletteProjectId);
                ConsoleUI.PrintSuccess($"MapRoulette project {system.MaprouletteProjectId} validated.");
                projectValidForTasks = true;
            }
            catch (Exception ex)
            {
                systemLogger.Error(ex, "Maproulette project validation error. ProjectId: {ProjectId}. Aborting.",
                    system.MaprouletteProjectId);
                ConsoleUI.PrintError($"MapRoulette project validation error: {ex.Message}");
                return;
            }
        }
        else
        {
            systemLogger.Warning("No Maproulette project configured. Action: SkippingTaskCreation");
            ConsoleUI.PrintWarning("No MapRoulette project configured - task creation will be skipped.");
        }

        var validationResult = _systemSetup.ValidateSystem(system.Name, throwOnMissing: false);
        if (!validationResult.IsValid)
        {
            systemLogger.Warning("System setup validation failed. Issue: {Issue}, Action: AttemptingAutoCreate",
                validationResult.ErrorMessage);
            ConsoleUI.PrintWarning($"System setup incomplete ({validationResult.ErrorMessage}); will auto-create missing files.");
        }

        try
        {
            await RunBikeShareLocationComparison(system, projectValidForTasks);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during run for {Name}", system.Name);
            ConsoleUI.PrintError($"Fatal error during run for {system.Name}: {ex.Message}");
        }
    }

    public async Task ValidateSystemAsync(int systemId)
    {
        ConsoleUI.PrintStep($"Validating system setup {systemId}");
        Log.Information("Validating system setup {SystemId}", systemId);
        try
        {
            var system = await _systemLoader.LoadByIdAsync(systemId);
            Log.Information("System configuration loaded: {Name} ({City})", system.Name, system.City);
            ConsoleUI.PrintSuccess($"System loaded: {system.Name} ({system.City})");

            SystemSetupHelper.ValidateSystemSetup(system.Name, throwOnMissing: true);
            Log.Information("System directory and files validated for {Name}", system.Name);
            ConsoleUI.PrintSuccess("Directory and files validated.");

            _systemSetup.ValidateInstructionFiles(system.Name);
            Log.Information("Instruction files validated for {Name}", system.Name);
            ConsoleUI.PrintSuccess("Instruction files validated.");

            if (system.MaprouletteProjectId > 0)
            {
                var projectValid = await _maproulette.ValidateProjectAsync(system.MaprouletteProjectId);
                if (projectValid)
                {
                    Log.Information("Maproulette project {ProjectId} validated", system.MaprouletteProjectId);
                    ConsoleUI.PrintSuccess($"MapRoulette project {system.MaprouletteProjectId} validated.");
                }
            }
            else
            {
                Log.Warning("No Maproulette project configured for {Name} - task creation skipped", system.Name);
                ConsoleUI.PrintWarning($"No MapRoulette project configured for {system.Name} - task creation skipped.");
            }
            Log.Information("All validations passed for {Name}. System ready.", system.Name);
            ConsoleUI.PrintSuccess($"All validations passed for {system.Name}. System ready.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Validation failed for system {SystemId}", systemId);
            ConsoleUI.PrintError($"Validation failed for system {systemId}: {ex.Message}");
        }
    }

    public async Task TestProjectAsync(int projectId)
    {
        ConsoleUI.PrintStep($"Validating MapRoulette project {projectId}");
        Log.Information("Validating Maproulette project {ProjectId}", projectId);
        try
        {
            var isValid = await _maproulette.ValidateProjectAsync(projectId);
            if (isValid)
            {
                Log.Information("Project {ProjectId} validation succeeded. ID can be used in configuration.", projectId);
                ConsoleUI.PrintSuccess($"Project {projectId} validated. ID can be used in configuration.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Project validation failed for {ProjectId}.", projectId);
            ConsoleUI.PrintError($"Project {projectId} validation failed: {ex.Message}");
        }
    }

    private async Task RunBikeShareLocationComparison(BikeShareSystem system, bool projectValidForTasks)
    {
        var summary = new FlowSummary();

        bool newlyCreated = false;
        try
        {
            newlyCreated = await _systemSetup.EnsureAsync(system.Name, system.Name, system.Name, system.City);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting up system files for {Name}.", system.Name);
            ConsoleUI.PrintError($"Error setting up system files for {system.Name}: {ex.Message}");
            throw;
        }

        if (newlyCreated)
        {
            Log.Information("Initial scaffold created for {Name}. Re-run the command to fetch and compare data after reviewing instructions.", system.Name);
            ConsoleUI.PrintSuccess($"Initial scaffold created for {system.Name}.");
            ConsoleUI.PrintAction($"Review the instructions in data_results/{system.Name}/ and re-run this command to fetch and compare data.");
            return; // Do not proceed further on the initial scaffolding run
        }

        var geojsonFilePath = _paths.GetSystemFullPath(system.Name, "bikeshare.geojson");
        var lastSyncDate = _git.GetLastCommitDate(geojsonFilePath);
        bool isNewSystem = lastSyncDate == null;

        if (isNewSystem)
        {
            Log.Information("No previous bikeshare.geojson found for {Name}. Treating as new system.", system.Name);
            ConsoleUI.PrintInfo($"No previous bikeshare.geojson found - treating {system.Name} as a new system.");
            lastSyncDate = DateTime.Now;
        }
        else
        {
            Log.Information("Last sync date for {Name}: {LastSync}", system.Name, lastSyncDate);
            ConsoleUI.PrintInfo($"Last sync: {lastSyncDate:yyyy-MM-dd HH:mm}");
        }

        ConsoleUI.PrintStep($"Fetching GBFS stations for {system.Name}");
        List<GeoPoint> locationsList;
        try
        {
            locationsList = await _bikeShareFetcher.FetchStationsAsync(system.GetStationInformationUrl());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed fetching bike share data for {Name}. URL={Url}", system.Name, system.GetStationInformationUrl());
            ConsoleUI.PrintError($"Failed fetching GBFS data for {system.Name}: {ex.Message}");
            throw;
        }
        ConsoleUI.PrintSuccess($"Fetched {locationsList.Count} GBFS stations.");

        // Apply optional station name prefix from configuration if provided
        var appliedCount = StationNamePrefixer.Apply(locationsList, system.StationNamePrefix);
        if (appliedCount > 0)
        {
            Log.Information("Applied station name prefix '{Prefix}' to {Count} stations for {Name}", system.StationNamePrefix, appliedCount, system.Name);
            ConsoleUI.PrintInfo($"Applied prefix '{system.StationNamePrefix}' to {appliedCount} stations.");
        }

        await _geoWriter.WriteMainAsync(locationsList, system.Name);
        await CompareAndGenerateDiffFiles(locationsList, system, isNewSystem, summary);
        var osmOk = await CompareWithOSMData(locationsList, system, summary);
        if (!osmOk)
        {
            return;
        }

        // Check for duplicate ref values and prompt to create tasks
        var duplicatesFile = _paths.GetSystemFullPath(system.Name, "bikeshare_osm_duplicates.geojson");
        summary.DuplicatesFileExists = File.Exists(duplicatesFile);
        if (summary.DuplicatesFileExists && system.MaprouletteProjectId > 0 && projectValidForTasks)
        {
            var confirmDuplicates = _prompt.ReadConfirmation("Duplicate ref values found in OSM. Create MapRoulette tasks to fix them?", 'n');
            if (confirmDuplicates.ToString().ToLower() == "y")
            {
                try
                {
                    // Ensure duplicates instruction file exists (for existing systems)
                    await _systemSetup.EnsureDuplicatesInstructionFileAsync(system.Name);

                    await _maproulette.CreateDuplicateTasksAsync(system.MaprouletteProjectId, system.Name);
                    Log.Information("Duplicate detection tasks created successfully");
                    ConsoleUI.PrintSuccess("Duplicate detection tasks created.");
                    summary.DuplicateTasksCreated = true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error creating duplicate detection tasks for {Name}", system.Name);
                    ConsoleUI.PrintError($"Error creating duplicate detection tasks: {ex.Message}");
                }
            }
            else
            {
                Log.Information("User declined duplicate detection task creation.");
                ConsoleUI.PrintInfo("Duplicate detection task creation skipped.");
            }
        }

        // Gate task creation entirely if no valid project configured
        if (!(system.MaprouletteProjectId > 0 && projectValidForTasks))
        {
            Log.Warning("Skipping Maproulette task creation for {Name} (no valid project).", system.Name);
            ConsoleUI.PrintWarning($"Skipping MapRoulette task creation for {system.Name} (no valid project).");
            PrintOperatorChecklist(system, summary);
            return;
        }

        var confirm = _prompt.ReadConfirmation("Create Maproulette tasks for new locations?", 'n');
        if (confirm.ToString().ToLower() != "y")
        {
            Log.Information("User declined task creation.");
            ConsoleUI.PrintInfo("Task creation skipped.");
            PrintOperatorChecklist(system, summary);
            return;
        }

        try
        {
            _systemSetup.ValidateInstructionFiles(system.Name);
            await _maproulette.CreateTasksAsync(system.MaprouletteProjectId, lastSyncDate ?? DateTime.UtcNow, system.Name, isNewSystem);
            summary.NewLocationTasksCreated = true;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("instruction files"))
        {
            Log.Error(ex, "Instruction file issue prevented task creation for {Name}", system.Name);
            ConsoleUI.PrintError($"Instruction file issue prevented task creation: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating Maproulette tasks for {Name}", system.Name);
            ConsoleUI.PrintError($"Error creating MapRoulette tasks for {system.Name}: {ex.Message}");
            throw;
        }

        PrintOperatorChecklist(system, summary);
    }

    private async Task CompareAndGenerateDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system, bool isNewSystem, FlowSummary summary)
    {
        ConsoleUI.PrintStep($"Comparing with last committed version of {system.Name}");
        Log.Information("Comparing current data with last committed version for {Name}", system.Name);
        try
        {
            var geojsonFile = _paths.GetSystemFullPath(system.Name, "bikeshare.geojson");
            string lastCommittedVersion = _git.GetLastCommittedVersion(geojsonFile);
            List<GeoPoint> lastCommittedPoints = lastCommittedVersion
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(GeoPoint.ParseLine)
                .ToList();

            var moveThreshold = system.GetMoveThresholdMeters();
            Log.Debug("Using move threshold: {Threshold}m for {Name}", moveThreshold, system.Name);
            var (addedPoints, removedPoints, movedPoints, renamedPoints) = _comparer.Compare(currentPoints, lastCommittedPoints, moveThreshold);

            await _geoWriter.WriteDiffAsync(addedPoints, removedPoints, movedPoints, renamedPoints, system.Name);
            Log.Information("Diff summary for {Name}: Added={Added} Removed={Removed} Moved={Moved} Renamed={Renamed}", system.Name, addedPoints.Count, removedPoints.Count, movedPoints.Count, renamedPoints.Count);
            ConsoleUI.PrintSuccess("Diff vs last committed version:");
            ConsoleUI.PrintStat("added", addedPoints.Count);
            ConsoleUI.PrintStat("removed", removedPoints.Count);
            ConsoleUI.PrintStat("moved", movedPoints.Count);
            ConsoleUI.PrintStat("renamed", renamedPoints.Count);

            summary.GbfsAddedVsGit = addedPoints.Count;
            summary.GbfsRemovedVsGit = removedPoints.Count;
            summary.GbfsMovedVsGit = movedPoints.Count;
            summary.GbfsRenamedVsGit = renamedPoints.Count;
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains("not found in git repository"))
        {
            Log.Warning("No previous version in git for {Name}; treating all stations as added.", system.Name);
            ConsoleUI.PrintWarning($"No previous version in git for {system.Name}; treating all stations as added.");
            await GenerateNewSystemDiffFiles(currentPoints, system, summary);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Git comparison failed for {Name}; treating all stations as added.", system.Name);
            ConsoleUI.PrintError($"Git comparison failed ({ex.Message}); treating all stations as added.");
            await GenerateNewSystemDiffFiles(currentPoints, system, summary);
        }
    }

    private async Task GenerateNewSystemDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system, FlowSummary summary)
    {
        var emptyList = new List<GeoPoint>();
        var emptyTupleList = new List<(GeoPoint current, GeoPoint old)>();
        await _geoWriter.WriteDiffAsync(currentPoints, emptyList, emptyList, emptyTupleList, system.Name);
        Log.Information("Generated diff files for new system {Name}: {Count} added", system.Name, currentPoints.Count);
        ConsoleUI.PrintSuccess($"New system {system.Name}: {currentPoints.Count} stations recorded as added.");
        summary.GbfsAddedVsGit = currentPoints.Count;
    }

    private async Task<bool> CompareWithOSMData(List<GeoPoint> bikeshareApiPoints, BikeShareSystem system, FlowSummary summary)
    {
        ConsoleUI.PrintStep($"Fetching OSM stations for {system.Name}");
        Log.Information("Fetching OSM stations for {Name}", system.Name);
        List<GeoPoint> osmPoints;
        try
        {
            await _osmFetcher.EnsureStationsFileAsync(system.Name, system.City);
            osmPoints = await _osmFetcher.FetchOsmStationsAsync(system.Name, system.City);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSM API fetch failed for {Name}. bikeshare.geojson has already been overwritten; run 'dotnet run -- reset {SystemId}' to restore the previous state before retrying.",
                system.Name, system.Id);
            ConsoleUI.PrintError($"OSM API fetch failed for {system.Name}: {ex.Message}");
            ConsoleUI.PrintAction($"bikeshare.geojson has already been overwritten. Run: dotnet run -- reset {system.Id}");
            return false;
        }
        Log.Information("Fetched {Count} OSM stations for {Name}", osmPoints.Count, system.Name);
        ConsoleUI.PrintSuccess($"Fetched {osmPoints.Count} OSM stations.");

        // Generate enhanced duplicate report with GBFS data for comparison
        await OSMDataFetcher.GenerateEnhancedDuplicateReportAsync(osmPoints, bikeshareApiPoints, system.Name);

        var osmComparisonThreshold = system.GetOsmComparisonThresholdMeters();
        Log.Debug("Using OSM comparison threshold: {Threshold}m for {Name}", osmComparisonThreshold, system.Name);
        var (missingInOSM, extraInOSM, differentInOSM, renamedInOSM) = _comparer.Compare(bikeshareApiPoints, osmPoints, osmComparisonThreshold);
        await _geoWriter.WriteOsmCompareAsync(missingInOSM, extraInOSM, differentInOSM, renamedInOSM, system.Name);
        await _osmChangeWriter.WriteRenameChangesAsync(renamedInOSM, system.Name);
        Log.Information("OSM comparison for {Name}: Missing={Missing} Extra={Extra} Moved={Moved} Renamed={Renamed}", system.Name, missingInOSM.Count, extraInOSM.Count, differentInOSM.Count, renamedInOSM.Count);
        ConsoleUI.PrintSuccess("GBFS vs OSM comparison:");
        ConsoleUI.PrintStat("missing in OSM", missingInOSM.Count);
        ConsoleUI.PrintStat("extra in OSM", extraInOSM.Count);
        ConsoleUI.PrintStat("moved", differentInOSM.Count);
        ConsoleUI.PrintStat("renamed", renamedInOSM.Count);

        summary.MissingInOsm = missingInOSM.Count;
        summary.ExtraInOsm = extraInOSM.Count;
        summary.MovedInOsm = differentInOSM.Count;
        summary.RenamedInOsm = renamedInOSM.Count;
        return true;
    }

    private static void PrintOperatorChecklist(BikeShareSystem system, FlowSummary summary)
    {
        var items = new List<string>();

        if (summary.RenamedInOsm > 0)
        {
            items.Add($"Load data_results/{system.Name}/bikeshare_renames.osc in JOSM, verify, and upload — {summary.RenamedInOsm} rename(s) pending.");
        }

        if (summary.DuplicatesFileExists)
        {
            if (summary.DuplicateTasksCreated)
            {
                items.Add($"Complete the MapRoulette duplicate-ref tasks created for {system.Name}.");
            }
            else
            {
                items.Add($"Review data_results/{system.Name}/bikeshare_osm_duplicates.geojson and resolve duplicate ref values in OSM.");
            }
        }

        if (summary.MissingInOsm > 0)
        {
            if (summary.NewLocationTasksCreated)
            {
                items.Add($"Complete MapRoulette tasks for {summary.MissingInOsm} station(s) missing in OSM.");
            }
            else
            {
                items.Add($"Add {summary.MissingInOsm} station(s) missing in OSM (see bikeshare_missing_in_osm.geojson) — no MapRoulette tasks were created.");
            }
        }

        if (summary.ExtraInOsm > 0)
        {
            items.Add($"Review bikeshare_extra_in_osm.geojson — {summary.ExtraInOsm} OSM station(s) not present in GBFS.");
        }

        if (summary.MovedInOsm > 0)
        {
            items.Add($"Review bikeshare_moved_in_osm.geojson — {summary.MovedInOsm} station(s) moved vs OSM.");
        }

        if (summary.HasGbfsDiff)
        {
            items.Add($"Commit updated data_results/{system.Name}/bikeshare.geojson as the next baseline.");
        }

        ConsoleUI.PrintChecklist($"Next steps for {system.Name}:", items);
    }

    private sealed class FlowSummary
    {
        public int GbfsAddedVsGit { get; set; }
        public int GbfsRemovedVsGit { get; set; }
        public int GbfsMovedVsGit { get; set; }
        public int GbfsRenamedVsGit { get; set; }
        public int MissingInOsm { get; set; }
        public int ExtraInOsm { get; set; }
        public int MovedInOsm { get; set; }
        public int RenamedInOsm { get; set; }
        public bool DuplicatesFileExists { get; set; }
        public bool DuplicateTasksCreated { get; set; }
        public bool NewLocationTasksCreated { get; set; }

        public bool HasGbfsDiff => GbfsAddedVsGit + GbfsRemovedVsGit + GbfsMovedVsGit + GbfsRenamedVsGit > 0;
    }
}
