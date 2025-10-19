using Serilog;
using Serilog.Events;
using Spectre.Console;

namespace prepareBikeParking;

/// <summary>
/// Handles console UI and logging configuration
/// </summary>
public static class ConsoleUI
{
    public static bool IsVerbose { get; set; }
    public static bool IsQuiet { get; set; }

    /// <summary>
    /// Reconfigure Serilog based on verbosity settings
    /// </summary>
    public static void ConfigureLogging()
    {
        var config = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With<Logging.SystemContextEnricher>()
            .WriteTo.File("logs/bikeshare-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(new Serilog.Formatting.Compact.CompactJsonFormatter(),
                "logs/metrics-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7);

        if (IsVerbose)
        {
            // Verbose: Show everything with correlation IDs and timestamps
            config.MinimumLevel.Debug()
                  .MinimumLevel.Override("System", LogEventLevel.Information)
                  .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }
        else if (IsQuiet)
        {
            // Quiet: Only errors and warnings to console
            config.MinimumLevel.Warning()
                  .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}{Exception}");
        }
        else
        {
            // Normal: Clean output without timestamps/correlation IDs
            config.MinimumLevel.Information()
                  .MinimumLevel.Override("System", LogEventLevel.Warning)
                  .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = config.CreateLogger();
    }

    /// <summary>
    /// Print a section header
    /// </summary>
    public static void PrintHeader(string title)
    {
        if (IsQuiet) return;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold cyan]{title}[/]").RuleStyle("cyan dim").LeftJustified());
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Print a success message
    /// </summary>
    public static void PrintSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Print a warning message
    /// </summary>
    public static void PrintWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Print an error message
    /// </summary>
    public static void PrintError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    /// <summary>
    /// Print an info message
    /// </summary>
    public static void PrintInfo(string message)
    {
        if (IsQuiet) return;
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    }
}
