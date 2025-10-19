using prepareBikeParking.Services;
using Spectre.Console;

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
    public Task CreateDuplicateTasksAsync(int projectId, string systemName) => MaprouletteTaskCreator.CreateDuplicateTasksAsync(projectId, systemName);
}

public class SystemSetupService : ISystemSetupService
{
    public Task<bool> EnsureAsync(string systemName, string operatorName, string brandName, string? cityName = null) => SystemSetupHelper.EnsureSystemSetUpAsync(systemName, operatorName, brandName, cityName);
    public void ValidateInstructionFiles(string systemName) => SystemSetupHelper.ValidateInstructionFilesForTaskCreation(systemName);
    public SystemValidationResult ValidateSystem(string systemName, bool throwOnMissing = false) => SystemSetupHelper.ValidateSystemSetup(systemName, throwOnMissing);
    public Task EnsureDuplicatesInstructionFileAsync(string systemName) => SystemSetupHelper.EnsureDuplicatesInstructionFileAsync(systemName);
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
        // In quiet mode, auto-decline without prompting
        if (ConsoleUI.IsQuiet)
        {
            Serilog.Log.Debug("Quiet mode: auto-declining prompt");
            return defaultAnswer;
        }

        // Check if we're in an interactive console
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Serilog.Log.Warning("Non-interactive mode detected. Auto-declining prompt: {Message}", message);
            AnsiConsole.MarkupLine($"[yellow]⚠[/] Non-interactive mode: auto-declining - {Markup.Escape(message)}");
            return defaultAnswer;
        }

        // Interactive mode: use Spectre.Console for better prompts
        var defaultChoice = defaultAnswer == 'y' ? "y" : "n";
        var prompt = new TextPrompt<string>($"[cyan]?[/] {Markup.Escape(message)}")
            .AddChoice("y")
            .AddChoice("n")
            .DefaultValue(defaultChoice)
            .ShowChoices(true)
            .ShowDefaultValue(true);

        try
        {
            var response = AnsiConsole.Prompt(prompt);
            Serilog.Log.Debug("Prompt response: {Response}", response);
            return response.ToLowerInvariant()[0];
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Error reading confirmation, using default: {Default}", defaultAnswer);
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
