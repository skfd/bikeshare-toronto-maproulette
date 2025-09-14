using prepareBikeParking.Services;

namespace prepareBikeParking.ServicesImpl;

public class BikeShareDataFetcherService : IBikeShareDataFetcher
{
    private readonly BikeShareDataFetcher _impl;
    public BikeShareDataFetcherService() : this(new BikeShareDataFetcher()) {}
    public BikeShareDataFetcherService(BikeShareDataFetcher impl)
    {
        _impl = impl;
    }
    public Task<List<GeoPoint>> FetchStationsAsync(string url) => _impl.FetchFromApiAsync(url);
}

public class OsmDataFetcherService : IOSMDataFetcher
{
    private readonly OSMDataFetcher _impl;
    public OsmDataFetcherService() : this(new OSMDataFetcher()) {}
    public OsmDataFetcherService(OSMDataFetcher impl) { _impl = impl; }
    public async Task<List<GeoPoint>> FetchOsmStationsAsync(string systemName, string cityName)
    {
        await OSMDataFetcher.EnsureStationsOverpassFileAsync(systemName, cityName);
        return await _impl.FetchFromOverpassApiAsync(systemName);
    }

    public Task EnsureStationsFileAsync(string systemName, string cityName) => OSMDataFetcher.EnsureStationsOverpassFileAsync(systemName, cityName);
}

public class GeoJsonWriterService : IGeoJsonWriter
{
    public Task WriteMainAsync(List<GeoPoint> points, string systemName) => GeoJsonGenerator.GenerateMainFileAsync(points, systemName);
    public Task WriteDiffAsync(List<GeoPoint> added, List<GeoPoint> removed, List<GeoPoint> moved, List<(GeoPoint current, GeoPoint old)> renamed, string systemName) => GeoJsonGenerator.GenerateDiffFilesAsync(added, removed, moved, renamed, systemName);
    public Task WriteOsmCompareAsync(List<GeoPoint> missing, List<GeoPoint> extra, List<GeoPoint> moved, List<(GeoPoint current, GeoPoint old)> renamed, string systemName) => GeoJsonGenerator.GenerateOSMComparisonFilesAsync(missing, extra, moved, renamed, systemName);
}

public class ComparerService : IComparerService
{
    public (List<GeoPoint> added, List<GeoPoint> removed, List<GeoPoint> moved, List<(GeoPoint current, GeoPoint old)> renamed) Compare(List<GeoPoint> currentPoints, List<GeoPoint> previousPoints, double moveThreshold)
        => BikeShareComparer.ComparePoints(currentPoints, previousPoints, moveThreshold);
}

public class GitReaderService : IGitReader
{
    public string GetLastCommittedVersion(string filePath) => GitDiffToGeojson.GetLastCommittedVersion(filePath);
    public DateTime? GetLastCommitDate(string filePath) => GitFunctions.GetLastCommitDateForFile(filePath);
}

public class MaprouletteService : IMaprouletteService
{
    public Task<bool> ValidateProjectAsync(int projectId) => MaprouletteTaskCreator.ValidateProjectAsync(projectId);
    public Task CreateTasksAsync(int projectId, DateTime lastSyncDate, string systemName, bool isNewSystem) => MaprouletteTaskCreator.CreateTasksAsync(projectId, lastSyncDate, systemName, isNewSystem);
}

public class SystemSetupService : ISystemSetupService
{
    public Task<bool> EnsureAsync(string systemName, string operatorName, string brandName, string? cityName = null) => SystemSetupHelper.EnsureSystemSetUpAsync(systemName, operatorName, brandName, cityName);
    public void ValidateInstructionFiles(string systemName) => SystemSetupHelper.ValidateInstructionFilesForTaskCreation(systemName);
    public SystemValidationResult ValidateSystem(string systemName, bool throwOnMissing = false) => SystemSetupHelper.ValidateSystemSetup(systemName, throwOnMissing);
}

public class FilePathProvider : IFilePathProvider
{
    public string GetSystemFullPath(string systemName, string fileName) => FileManager.GetSystemFullPath(systemName, fileName);
    public bool SystemFileExists(string systemName, string fileName) => FileManager.SystemFileExists(systemName, fileName);
}

public class PromptService : IPromptService
{
    public char ReadConfirmation(string message, char defaultAnswer = 'n')
    {
        Serilog.Log.Information(message + " (y/N)");
        try
        {
            var key = Console.ReadKey(intercept: true).KeyChar;
            Serilog.Log.Debug("Prompt response {Key}", key);
            return key == '\0' ? defaultAnswer : key;
        }
        catch
        {
            return defaultAnswer;
        }
    }
}

public class BikeShareSystemLoaderService : IBikeShareSystemLoader
{
    public Task<BikeShareSystem> LoadByIdAsync(int id) => BikeShareSystemLoader.LoadSystemByIdAsync(id);
    public Task ListAsync() => BikeShareSystemLoader.ListAvailableSystemsAsync();
}

public class OsmChangeWriterService : IOsmChangeWriter
{
    public Task WriteRenameChangesAsync(List<(GeoPoint current, GeoPoint old)> renamed, string systemName) => OsmFileFunctions.GenerateRenameOsmChangeFile(renamed, systemName);
}
