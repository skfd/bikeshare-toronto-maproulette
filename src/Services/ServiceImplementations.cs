using prepareBikeParking.Services;

namespace prepareBikeParking.ServicesImpl;

public class BikeShareDataFetcherService : IBikeShareDataFetcher
{
    public Task<List<GeoPoint>> FetchStationsAsync(string url) => BikeShareDataFetcher.FetchFromApiAsync(url);
}

public class OsmDataFetcherService : IOSMDataFetcher
{
    public async Task<List<GeoPoint>> FetchOsmStationsAsync(string systemName, string cityName)
    {
        await OSMDataFetcher.EnsureStationsOverpassFileAsync(systemName, cityName);
        return await OSMDataFetcher.FetchFromOverpassApiAsync(systemName);
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
    public Task EnsureAsync(string systemName, string operatorName, string brandName, string? cityName = null) => SystemSetupHelper.EnsureSystemSetUpAsync(systemName, operatorName, brandName, cityName);
    public void ValidateInstructionFiles(string systemName) => SystemSetupHelper.ValidateInstructionFilesForTaskCreation(systemName);
}

public class FilePathProvider : IFilePathProvider
{
    public string GetSystemFullPath(string systemName, string fileName) => FileManager.GetSystemFullPath(systemName, fileName);
    public bool SystemFileExists(string systemName, string fileName) => FileManager.SystemFileExists(systemName, fileName);
}
