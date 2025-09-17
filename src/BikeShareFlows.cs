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
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed loading system configuration. SystemId: {SystemId}", systemId);
            return;
        }

        var systemLogger = Log.Logger.ForBikeShareSystem(system.Name, system.Id);
        systemLogger.Information("Starting comparison run. City: {City}, MaprouletteProject: {ProjectId}",
            system.City, system.MaprouletteProjectId);
        systemLogger.Debug("System endpoints configured. GbfsApi: {Api}, StationInfoUrl: {StationUrl}",
            system.GbfsApi, system.GetStationInformationUrl());

        var projectValidForTasks = false;
        if (system.MaprouletteProjectId > 0)
        {
            systemLogger.Information("Validating Maproulette project. ProjectId: {ProjectId}", system.MaprouletteProjectId);
            try
            {
                var projectValid = await _maproulette.ValidateProjectAsync(system.MaprouletteProjectId);
                if (!projectValid)
                {
                    systemLogger.Warning("Maproulette project validation failed. ProjectId: {ProjectId}, Action: SkippingTaskCreation",
                        system.MaprouletteProjectId);
                }
                else
                {
                    systemLogger.Information("Maproulette project validated successfully. ProjectId: {ProjectId}",
                        system.MaprouletteProjectId);
                    projectValidForTasks = true;
                }
            }
            catch (Exception ex)
            {
                systemLogger.Error(ex, "Maproulette project validation error. ProjectId: {ProjectId}, Action: ContinuingWithoutTasks",
                    system.MaprouletteProjectId);
            }
        }
        else
        {
            systemLogger.Warning("No Maproulette project configured. Action: SkippingTaskCreation");
        }

        var validationResult = _systemSetup.ValidateSystem(system.Name, throwOnMissing: false);
        if (!validationResult.IsValid)
        {
            systemLogger.Warning("System setup validation failed. Issue: {Issue}, Action: AttemptingAutoCreate",
                validationResult.ErrorMessage);
        }

        try
        {
            await RunBikeShareLocationComparison(system, projectValidForTasks);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during run for {Name}", system.Name);
        }
    }

    public async Task ValidateSystemAsync(int systemId)
    {
        Log.Information("Validating system setup {SystemId}", systemId);
        try
        {
            var system = await _systemLoader.LoadByIdAsync(systemId);
            Log.Information("System configuration loaded: {Name} ({City})", system.Name, system.City);
            SystemSetupHelper.ValidateSystemSetup(system.Name, throwOnMissing: true);
            Log.Information("System directory and files validated for {Name}", system.Name);
            _systemSetup.ValidateInstructionFiles(system.Name);
            Log.Information("Instruction files validated for {Name}", system.Name);
            if (system.MaprouletteProjectId > 0)
            {
                var projectValid = await _maproulette.ValidateProjectAsync(system.MaprouletteProjectId);
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

    public async Task TestProjectAsync(int projectId)
    {
        Log.Information("Validating Maproulette project {ProjectId}", projectId);
        try
        {
            var isValid = await _maproulette.ValidateProjectAsync(projectId);
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

    private async Task RunBikeShareLocationComparison(BikeShareSystem system, bool projectValidForTasks)
    {
        bool newlyCreated = false;
        try
        {
            newlyCreated = await _systemSetup.EnsureAsync(system.Name, system.Name, system.Name, system.City);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting up system files for {Name}.", system.Name);
            throw;
        }

        if (newlyCreated)
        {
            Log.Information("Initial scaffold created for {Name}. Re-run the command to fetch and compare data after reviewing instructions.", system.Name);
            return; // Do not proceed further on the initial scaffolding run
        }

        var geojsonFilePath = _paths.GetSystemFullPath(system.Name, "bikeshare.geojson");
        var lastSyncDate = _git.GetLastCommitDate(geojsonFilePath);
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
            locationsList = await _bikeShareFetcher.FetchStationsAsync(system.GetStationInformationUrl());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed fetching bike share data for {Name}. URL={Url}", system.Name, system.GetStationInformationUrl());
            throw;
        }

        // Apply optional station name prefix from configuration if provided
        var appliedCount = StationNamePrefixer.Apply(locationsList, system.StationNamePrefix);
        if (appliedCount > 0)
        {
            Log.Information("Applied station name prefix '{Prefix}' to {Count} stations for {Name}", system.StationNamePrefix, appliedCount, system.Name);
        }

        await _geoWriter.WriteMainAsync(locationsList, system.Name);
        await CompareAndGenerateDiffFiles(locationsList, system, isNewSystem);
        await CompareWithOSMData(locationsList, system);

        // Gate task creation entirely if no valid project configured
        if (!(system.MaprouletteProjectId > 0 && projectValidForTasks))
        {
            Log.Warning("Skipping Maproulette task creation for {Name} (no valid project).", system.Name);
            return;
        }

        var confirm = _prompt.ReadConfirmation("Create Maproulette tasks for new locations?", 'n');
        if (confirm.ToString().ToLower() != "y")
        {
            Log.Information("User declined task creation.");
            return;
        }

        try
        {
            _systemSetup.ValidateInstructionFiles(system.Name);
            await _maproulette.CreateTasksAsync(system.MaprouletteProjectId, lastSyncDate ?? DateTime.UtcNow, system.Name, isNewSystem);
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

    private async Task CompareAndGenerateDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system, bool isNewSystem = false)
    {
        Log.Information("Comparing current data with last committed version for {Name}", system.Name);
        try
        {
            var geojsonFile = _paths.GetSystemFullPath(system.Name, "bikeshare.geojson");
            string lastCommittedVersion = _git.GetLastCommittedVersion(geojsonFile);
            List<GeoPoint> lastCommittedPoints = lastCommittedVersion
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(GeoPoint.ParseLine)
                .ToList();

            var (addedPoints, removedPoints, movedPoints, renamedPoints) = _comparer.Compare(currentPoints, lastCommittedPoints, moveThreshold: 3);

            await _geoWriter.WriteDiffAsync(addedPoints, removedPoints, movedPoints, renamedPoints, system.Name);
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

    private async Task GenerateNewSystemDiffFiles(List<GeoPoint> currentPoints, BikeShareSystem system)
    {
        var emptyList = new List<GeoPoint>();
        var emptyTupleList = new List<(GeoPoint current, GeoPoint old)>();
        await _geoWriter.WriteDiffAsync(currentPoints, emptyList, emptyList, emptyTupleList, system.Name);
        Log.Information("Generated diff files for new system {Name}: {Count} added", system.Name, currentPoints.Count);
    }

    private async Task CompareWithOSMData(List<GeoPoint> bikeshareApiPoints, BikeShareSystem system)
    {
        try
        {
            Log.Information("Fetching OSM stations for {Name}", system.Name);
            await _osmFetcher.EnsureStationsFileAsync(system.Name, system.City);
            var osmPoints = await _osmFetcher.FetchOsmStationsAsync(system.Name, system.City);
            Log.Information("Fetched {Count} OSM stations for {Name}", osmPoints.Count, system.Name);
            var (missingInOSM, extraInOSM, differentInOSM, renamedInOSM) = _comparer.Compare(bikeshareApiPoints, osmPoints, moveThreshold: 30);
            await _geoWriter.WriteOsmCompareAsync(missingInOSM, extraInOSM, differentInOSM, renamedInOSM, system.Name);
            await _osmChangeWriter.WriteRenameChangesAsync(renamedInOSM, system.Name);
            Log.Information("OSM comparison for {Name}: Missing={Missing} Extra={Extra} Moved={Moved} Renamed={Renamed}", system.Name, missingInOSM.Count, extraInOSM.Count, differentInOSM.Count, renamedInOSM.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSM comparison failed for {Name} - continuing", system.Name);
        }
    }
}
