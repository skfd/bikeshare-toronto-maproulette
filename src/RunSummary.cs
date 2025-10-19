using Spectre.Console;

namespace prepareBikeParking;

/// <summary>
/// Displays a rich summary of the sync run
/// </summary>
public class RunSummary
{
    public string SystemName { get; set; } = "";
    public int StationsAdded { get; set; }
    public int StationsRemoved { get; set; }
    public int StationsMoved { get; set; }
    public int StationsRenamed { get; set; }
    public int OsmDuplicates { get; set; }
    public List<string> GeneratedFiles { get; set; } = new();
    public bool MapRouletteTasksCreated { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Print a rich summary to the console
    /// </summary>
    public void Print()
    {
        if (ConsoleUI.IsQuiet) return;

        var panel = new Panel(BuildSummaryContent())
        {
            Header = new PanelHeader($" [bold cyan]Summary: {Markup.Escape(SystemName)}[/] ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private string BuildSummaryContent()
    {
        var content = new System.Text.StringBuilder();

        // Changes detected section
        var totalChanges = StationsAdded + StationsRemoved + StationsMoved + StationsRenamed;

        if (totalChanges > 0)
        {
            content.AppendLine("[bold]Changes Detected:[/]");
            if (StationsAdded > 0)
                content.AppendLine($"  [green]+[/] {StationsAdded} station(s) added");
            if (StationsRemoved > 0)
                content.AppendLine($"  [red]-[/] {StationsRemoved} station(s) removed");
            if (StationsMoved > 0)
                content.AppendLine($"  [yellow]↔[/] {StationsMoved} station(s) moved");
            if (StationsRenamed > 0)
                content.AppendLine($"  [blue]✎[/] {StationsRenamed} station(s) renamed");
            content.AppendLine();
        }
        else
        {
            content.AppendLine("[bold]Changes Detected:[/]");
            content.AppendLine("  [grey]No changes detected[/]");
            content.AppendLine();
        }

        // OSM Data Quality section
        if (OsmDuplicates > 0)
        {
            content.AppendLine("[bold]OSM Data Quality:[/]");
            content.AppendLine($"  [yellow]⚠[/] {OsmDuplicates} duplicate ref value(s) found");
            content.AppendLine($"  [grey]→ Fix these in OpenStreetMap[/]");
            content.AppendLine();
        }

        // Files generated section
        if (GeneratedFiles.Count > 0)
        {
            content.AppendLine("[bold]Files Generated:[/]");
            foreach (var file in GeneratedFiles.Take(6))  // Show max 6 files
            {
                var fileName = Path.GetFileName(file);
                content.AppendLine($"  [grey]•[/] {Markup.Escape(fileName)}");
            }
            if (GeneratedFiles.Count > 6)
            {
                content.AppendLine($"  [grey]• ... and {GeneratedFiles.Count - 6} more[/]");
            }
            content.AppendLine();
        }

        // Next steps
        content.AppendLine("[bold]Next Steps:[/]");
        if (totalChanges > 0)
        {
            content.AppendLine("  [grey]1. Review changes in JOSM/QGIS[/]");
            if (OsmDuplicates > 0)
            {
                content.AppendLine("  [grey]2. Fix duplicate refs in OpenStreetMap[/]");
                content.AppendLine("  [grey]3. Update MapRoulette tasks if needed[/]");
            }
            else
            {
                content.AppendLine("  [grey]2. Update MapRoulette tasks if needed[/]");
            }
        }
        else
        {
            if (OsmDuplicates > 0)
            {
                content.AppendLine("  [grey]1. Fix duplicate refs in OpenStreetMap[/]");
            }
            else
            {
                content.AppendLine("  [grey]1. No action required - data is in sync[/]");
            }
        }

        // Performance stats
        content.AppendLine();
        content.AppendLine($"[grey]Completed in {Duration.TotalSeconds:F1}s[/]");

        return content.ToString().TrimEnd();
    }
}
