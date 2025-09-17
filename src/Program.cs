using System.CommandLine;
using Serilog;
using Serilog.Events;
using prepareBikeParking;
using Microsoft.Extensions.DependencyInjection;
using prepareBikeParking.Services;
using prepareBikeParking.ServicesImpl;
using prepareBikeParking.Logging;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.With<SystemContextEnricher>()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/bikeshare-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:O} [{Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(new Serilog.Formatting.Compact.CompactJsonFormatter(),
        "logs/metrics-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

using var correlationScope = CorrelationIdMiddleware.BeginCorrelationScope();

try
{
    Log.Information("Application started. Version: {Version}, Arguments: {Args}",
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
        args);

    var services = new ServiceCollection();
    services.AddSingleton<IBikeShareDataFetcher, BikeShareDataFetcherService>();
    services.AddSingleton<IOSMDataFetcher, OsmDataFetcherService>();
    services.AddSingleton<IGeoJsonWriter, GeoJsonWriterService>();
    services.AddSingleton<IComparerService, ComparerService>();
    services.AddSingleton<IGitReader, GitReaderService>();
    services.AddSingleton<IMaprouletteService, MaprouletteService>();
    services.AddSingleton<ISystemSetupService, SystemSetupService>();
    services.AddSingleton<IFilePathProvider, FilePathProvider>();
    services.AddSingleton<IPromptService, PromptService>();
    services.AddSingleton<IBikeShareSystemLoader, BikeShareSystemLoaderService>();
    services.AddSingleton<IOsmChangeWriter, OsmChangeWriterService>();
    services.AddSingleton<BikeShareFlows>();
    var provider = services.BuildServiceProvider();

    var root = new RootCommand("Bike Share Location Comparison Tool");

    // run command & root default
    var systemIdArg = new Argument<int>("system-id", description: "Numeric system ID from bikeshare_systems.json");
    var runCommand = new Command("run", "Run comparison for a system") { systemIdArg };
    runCommand.SetHandler(async (int id) => await provider.GetRequiredService<BikeShareFlows>().RunSystemFlow(id), systemIdArg);

    root.AddArgument(systemIdArg); // treat root invocation same as run
    root.SetHandler(async (int id) => await provider.GetRequiredService<BikeShareFlows>().RunSystemFlow(id), systemIdArg);

    // list systems
    var listCommand = new Command("list", "List available systems");
    listCommand.SetHandler(async () => await BikeShareSystemLoader.ListAvailableSystemsAsync());

    // validate system setup
    var validateSystemIdArg = new Argument<int>("system-id", "System ID to validate");
    var validateCommand = new Command("validate", "Validate system configuration & instructions") { validateSystemIdArg };
    validateCommand.SetHandler(async (int id) => await provider.GetRequiredService<BikeShareFlows>().ValidateSystemAsync(id), validateSystemIdArg);

    // test maproulette project
    var projectIdArg = new Argument<int>("project-id", "Maproulette project ID to test");
    var testProjectCommand = new Command("test-project", "Validate Maproulette project accessibility") { projectIdArg };
    testProjectCommand.SetHandler(async (int pid) => await provider.GetRequiredService<BikeShareFlows>().TestProjectAsync(pid), projectIdArg);


    // save-global-service command
    var saveGlobalServiceCommand = new Command("save-global-service", "Download and save the global GBFS service provider list");
    saveGlobalServiceCommand.SetHandler(async () =>
    {
        var filePath = Path.Combine("data_results", "global_gbfs_services.csv");
        await GlobalGbfsServiceFetcher.SaveGlobalServiceListAsync(filePath);
        Log.Information("Global GBFS service provider list saved to {Path}", filePath);
    });

    // fetch-brand-tags command
    var fetchBrandTagsSystemIdArg = new Argument<int?>("system-id", () => null, "System ID to fetch brand tags for (optional - fetches for all systems if not specified)");
    var fetchBrandTagsCommand = new Command("fetch-brand-tags", "Fetch OSM brand tags from Name Suggestion Index for bike share systems") { fetchBrandTagsSystemIdArg };
    fetchBrandTagsCommand.SetHandler(async (int? systemId) =>
    {
        if (systemId.HasValue)
        {
            // Fetch for specific system
            var system = await BikeShareSystemLoader.LoadSystemByIdAsync(systemId.Value);
            var success = await NameSuggestionIndexFetcher.FetchAndSaveOsmTagsForSystemAsync(system);
            if (!success)
            {
                Log.Warning("No OSM brand tags found for system {SystemId}: {Name}. Ensure brand:wikidata is set in bikeshare_systems.json", systemId.Value, system.Name);
            }
        }
        else
        {
            // Fetch for all systems
            var successCount = await NameSuggestionIndexFetcher.FetchAndSaveOsmTagsForAllSystemsAsync();
            Log.Information("Brand tags fetch completed for {Success} systems", successCount);
        }
    }, fetchBrandTagsSystemIdArg);

    root.AddCommand(runCommand);
    root.AddCommand(listCommand);
    root.AddCommand(validateCommand);
    root.AddCommand(testProjectCommand);
    root.AddCommand(saveGlobalServiceCommand);
    root.AddCommand(fetchBrandTagsCommand);

    // setup-system command (scaffolds only and exits)
    var setupSystemIdArg = new Argument<int>("system-id", description: "Numeric system ID to scaffold (no data fetch)");
    var setupCommand = new Command("setup", "Create instruction templates and overpass file for a system") { setupSystemIdArg };
    setupCommand.SetHandler(async (int id) => {
        try
        {
            var sys = await provider.GetRequiredService<IBikeShareSystemLoader>().LoadByIdAsync(id);
            var created = await SystemSetupHelper.EnsureSystemSetUpAsync(sys.Name, sys.Name, sys.Name, sys.City);
            if (created)
                Log.Information("Scaffolding created for {System}. You can now run comparison.", sys.Name);
            else
                Log.Information("System {System} already set up.", sys.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Setup failed for system {Id}", id);
        }
    }, setupSystemIdArg);
    root.AddCommand(setupCommand);

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
