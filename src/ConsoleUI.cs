using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Events;
using Spectre.Console;

namespace prepareBikeParking;

/// <summary>
/// Console output for end users. Separate from Serilog logging, which writes
/// structured records to files for diagnostics. Use ConsoleUI for anything the
/// human running the command should see; use Log.* for file-only diagnostics.
/// </summary>
public static class ConsoleUI
{
    public static bool IsVerbose { get; set; }
    public static bool IsQuiet { get; set; }

    /// <summary>
    /// Reconfigure Serilog. Normal/quiet: file sinks only. Verbose: also mirror
    /// to the console so developers can see structured details live.
    /// </summary>
    public static void ConfigureLogging()
    {
        var config = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With<Logging.SystemContextEnricher>()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("System", LogEventLevel.Information)
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
            config.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = config.CreateLogger();
    }

    /// <summary>Section header. Hidden in quiet mode.</summary>
    public static void PrintHeader(string title)
    {
        if (IsQuiet) return;
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold cyan]{Markup.Escape(title)}[/]").RuleStyle("cyan dim").LeftJustified());
        AnsiConsole.WriteLine();
    }

    /// <summary>Progress step (cyan). Hidden in quiet mode.</summary>
    public static void PrintStep(string message)
    {
        if (IsQuiet) return;
        AnsiConsole.MarkupLine($"[cyan]›[/] {Markup.Escape(message)}");
    }

    /// <summary>Completion of a step (green). Hidden in quiet mode.</summary>
    public static void PrintSuccess(string message)
    {
        if (IsQuiet) return;
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    /// <summary>Named count / stat line. Hidden in quiet mode.</summary>
    public static void PrintStat(string label, object value)
    {
        if (IsQuiet) return;
        AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(label)}:[/] [white]{Markup.Escape(value.ToString() ?? "")}[/]");
    }

    /// <summary>
    /// User must take an action. Use for prompts, suggested CLI commands,
    /// environment-variable setup, or other instructions the operator needs
    /// to notice. Bold magenta, visible even in quiet mode.
    /// </summary>
    public static void PrintAction(string message)
    {
        AnsiConsole.MarkupLine($"[bold magenta]→ {Markup.Escape(message)}[/]");
    }

    /// <summary>Non-fatal issue the user should see.</summary>
    public static void PrintWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] [yellow]{Markup.Escape(message)}[/]");
    }

    /// <summary>Failure the user should see. Always shown.</summary>
    public static void PrintError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] [red]{Markup.Escape(message)}[/]");
    }

    /// <summary>Secondary info (grey). Hidden in quiet mode.</summary>
    public static void PrintInfo(string message)
    {
        if (IsQuiet) return;
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    }

    /// <summary>
    /// Operator checklist printed at the end of a run. Each item is an action
    /// the operator still needs to take. Always shown (even in quiet mode) so
    /// nothing slips through.
    /// </summary>
    public static void PrintChecklist(string title, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold magenta]{Markup.Escape(title)}[/]");
        foreach (var item in list)
        {
            AnsiConsole.MarkupLine($"  [magenta][[ ]][/] {Markup.Escape(item)}");
        }
    }
}
