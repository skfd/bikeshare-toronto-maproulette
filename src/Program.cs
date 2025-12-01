using System.CommandLine;
using Serilog;
using Serilog.Events;
using prepareBikeParking;
using Microsoft.Extensions.DependencyInjection;
using prepareBikeParking.Services;
using prepareBikeParking.ServicesImpl;
using prepareBikeParking.Logging;
using Spectre.Console;

// Initial logger - will be reconfigured after parsing command line args
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.With<SystemContextEnricher>()
    .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
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

    // Global options for verbosity
    var verboseOption = new Option<bool>("--verbose", "-v") { Description = "Show detailed logging output" };
    var quietOption = new Option<bool>("--quiet", "-q") { Description = "Show minimal output (errors and summary only)" };

    root.Options.Add(verboseOption);
    root.Options.Add(quietOption);

    // run command & root default
    var systemIdArg = new Argument<int>("system-id") { Description = "Numeric system ID from bikeshare_systems.json" };
    var runCommand = new Command("run", "Run comparison for a system");
    runCommand.Arguments.Add(systemIdArg);
    runCommand.SetAction(async (ParseResult parseResult) =>
    {
        var id = parseResult.GetValue(systemIdArg);
        var verbose = parseResult.GetValue(verboseOption);
        var quiet = parseResult.GetValue(quietOption);
        ConsoleUI.IsVerbose = verbose;
        ConsoleUI.IsQuiet = quiet;
        ConsoleUI.ConfigureLogging();
        await provider.GetRequiredService<BikeShareFlows>().RunSystemFlow(id);
    });

    root.Arguments.Add(systemIdArg); // treat root invocation same as run
    root.SetAction(async (ParseResult parseResult) =>
    {
        var id = parseResult.GetValue(systemIdArg);
        var verbose = parseResult.GetValue(verboseOption);
        var quiet = parseResult.GetValue(quietOption);
        ConsoleUI.IsVerbose = verbose;
        ConsoleUI.IsQuiet = quiet;
        ConsoleUI.ConfigureLogging();
        await provider.GetRequiredService<BikeShareFlows>().RunSystemFlow(id);
    });

    // list systems
    var listCommand = new Command("list", "List available systems");
    listCommand.SetAction(async (ParseResult parseResult) => await BikeShareSystemLoader.ListAvailableSystemsAsync());

    // validate system setup
    var validateSystemIdArg = new Argument<int>("system-id") { Description = "System ID to validate" };
    var validateCommand = new Command("validate", "Validate system configuration & instructions");
    validateCommand.Arguments.Add(validateSystemIdArg);
    validateCommand.SetAction(async (ParseResult parseResult) => 
    {
        var id = parseResult.GetValue(validateSystemIdArg);
        await provider.GetRequiredService<BikeShareFlows>().ValidateSystemAsync(id);
    });

    // test maproulette project
    var projectIdArg = new Argument<int>("project-id") { Description = "Maproulette project ID to test" };
    var testProjectCommand = new Command("test-project", "Validate Maproulette project accessibility");
    testProjectCommand.Arguments.Add(projectIdArg);
    testProjectCommand.SetAction(async (ParseResult parseResult) => 
    {
        var pid = parseResult.GetValue(projectIdArg);
        await provider.GetRequiredService<BikeShareFlows>().TestProjectAsync(pid);
    });


    // save-global-service command
    var saveGlobalServiceCommand = new Command("save-global-service", "Download and save the global GBFS service provider list");
    saveGlobalServiceCommand.SetAction(async (ParseResult parseResult) =>
    {
        var filePath = Path.Combine("data_results", "global_gbfs_services.csv");
        await GlobalGbfsServiceFetcher.SaveGlobalServiceListAsync(filePath);
        Log.Information("Global GBFS service provider list saved to {Path}", filePath);
    });

    // fetch-brand-tags command
    var fetchBrandTagsSystemIdArg = new Argument<int?>("system-id") { Description = "System ID to fetch brand tags for (optional - fetches for all systems if not specified)" };
    var fetchBrandTagsCommand = new Command("fetch-brand-tags", "Fetch OSM brand tags from Name Suggestion Index for bike share systems");
    fetchBrandTagsCommand.Arguments.Add(fetchBrandTagsSystemIdArg);
    fetchBrandTagsCommand.SetAction(async (ParseResult parseResult) =>
    {
        var systemId = parseResult.GetValue(fetchBrandTagsSystemIdArg);
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
    });

    root.Subcommands.Add(runCommand);
    root.Subcommands.Add(listCommand);
    root.Subcommands.Add(validateCommand);
    root.Subcommands.Add(testProjectCommand);
    root.Subcommands.Add(saveGlobalServiceCommand);
    root.Subcommands.Add(fetchBrandTagsCommand);

    // setup-system command (scaffolds only and exits)
    var setupSystemIdArg = new Argument<int>("system-id") { Description = "Numeric system ID to scaffold (no data fetch)" };
    var setupCommand = new Command("setup", "Create instruction templates and overpass file for a system");
    setupCommand.Arguments.Add(setupSystemIdArg);
    setupCommand.SetAction(async (ParseResult parseResult) => {
        var id = parseResult.GetValue(setupSystemIdArg);
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
    });
    root.Subcommands.Add(setupCommand);

    var exitCode = await root.Parse(args).InvokeAsync();
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
