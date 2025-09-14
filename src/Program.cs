using System.CommandLine;
using Serilog;
using Serilog.Events;
using prepareBikeParking;
using Microsoft.Extensions.DependencyInjection;
using prepareBikeParking.Services;
using prepareBikeParking.ServicesImpl;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate: "[${Timestamp:HH:mm:ss} ${Level:u3}] ${Message:lj}${NewLine}${Exception}")
    .WriteTo.File("logs/bikeshare-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7,
        outputTemplate: "${Timestamp:O} [${Level:u3}] ${Message:lj} ${Properties:j}${NewLine}${Exception}")
    .CreateLogger();

try
{
    Log.Information("Startup arguments: {Args}", args);

    var services = new ServiceCollection();
    services.AddSingleton<IBikeShareDataFetcher, BikeShareDataFetcherService>();
    services.AddSingleton<IOSMDataFetcher, OsmDataFetcherService>();
    services.AddSingleton<IGeoJsonWriter, GeoJsonWriterService>();
    services.AddSingleton<IComparerService, ComparerService>();
    services.AddSingleton<IGitReader, GitReaderService>();
    services.AddSingleton<IMaprouletteService, MaprouletteService>();
    services.AddSingleton<ISystemSetupService, SystemSetupService>();
    services.AddSingleton<IFilePathProvider, FilePathProvider>();
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

    root.AddCommand(runCommand);
    root.AddCommand(listCommand);
    root.AddCommand(validateCommand);
    root.AddCommand(testProjectCommand);

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
