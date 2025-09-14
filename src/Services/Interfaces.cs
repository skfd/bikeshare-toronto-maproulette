namespace prepareBikeParking.Services;

public interface IBikeShareDataFetcher
{
    Task<List<GeoPoint>> FetchStationsAsync(string url);
}

public interface IOSMDataFetcher
{
    Task<List<GeoPoint>> FetchOsmStationsAsync(string systemName, string cityName);
    Task EnsureStationsFileAsync(string systemName, string cityName);
}

public interface IGeoJsonWriter
{
    Task WriteMainAsync(List<GeoPoint> points, string systemName);
    Task WriteDiffAsync(List<GeoPoint> added, List<GeoPoint> removed, List<GeoPoint> moved, List<(GeoPoint current, GeoPoint old)> renamed, string systemName);
    Task WriteOsmCompareAsync(List<GeoPoint> missing, List<GeoPoint> extra, List<GeoPoint> moved, List<(GeoPoint current, GeoPoint old)> renamed, string systemName);
}

public interface IComparerService
{
    (List<GeoPoint> added, List<GeoPoint> removed, List<GeoPoint> moved, List<(GeoPoint current, GeoPoint old)> renamed)
        Compare(List<GeoPoint> currentPoints, List<GeoPoint> previousPoints, double moveThreshold);
}

public interface IGitReader
{
    string GetLastCommittedVersion(string filePath);
    DateTime? GetLastCommitDate(string filePath);
}

public interface IMaprouletteService
{
    Task<bool> ValidateProjectAsync(int projectId);
    Task CreateTasksAsync(int projectId, DateTime lastSyncDate, string systemName, bool isNewSystem);
}

public interface ISystemSetupService
{
    Task EnsureAsync(string systemName, string operatorName, string brandName, string? cityName = null);
    void ValidateInstructionFiles(string systemName);
    SystemValidationResult ValidateSystem(string systemName, bool throwOnMissing = false);
}

public interface IFilePathProvider
{
    string GetSystemFullPath(string systemName, string fileName);
    bool SystemFileExists(string systemName, string fileName);
}

public interface IPromptService
{
    char ReadConfirmation(string message, char defaultAnswer = 'n');
}

public interface IBikeShareSystemLoader
{
    Task<BikeShareSystem> LoadByIdAsync(int id);
    Task ListAsync();
}

public interface IOsmChangeWriter
{
    Task WriteRenameChangesAsync(List<(GeoPoint current, GeoPoint old)> renamed, string systemName);
}
