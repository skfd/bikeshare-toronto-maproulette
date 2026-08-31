using System.Text;
using System.Text.Json;

namespace prepareBikeParking;

/// <summary>
/// Persists the outcome of a run to disk.
///
/// The operator checklist is the actual product of a sync: the JOSM uploads, the
/// duplicate refs, the ref conflicts. Printing it to the console was fine when a
/// human was watching the run; a scheduled run has nobody watching, so the
/// checklist has to outlive the process or the work items are simply lost.
///
/// Two files per system, written side by side with the GeoJSON outputs:
///   last_run.md   - the digest a human reads
///   last_run.json - the same facts for the workflow that assembles the issue
/// </summary>
public static class RunReport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed record Result(
        string System,
        string City,
        DateTime RanAtUtc,
        bool Succeeded,
        string? Error,
        IReadOnlyDictionary<string, int> Counts,
        IReadOnlyList<string> Checklist);

    /// <summary>
    /// Write both report files for a system. Never throws: a report-writing
    /// failure must not fail an otherwise good sync.
    /// </summary>
    public static void Write(Result result)
    {
        try
        {
            var jsonPath = FileManager.GetSystemFullPath(result.System, "last_run.json");
            var dir = Path.GetDirectoryName(jsonPath);
            if (dir != null) Directory.CreateDirectory(dir);

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions), Encoding.UTF8);
            File.WriteAllText(
                FileManager.GetSystemFullPath(result.System, "last_run.md"),
                BuildMarkdown(result),
                Encoding.UTF8);

            Serilog.Log.Information("Run report written for {System}", result.System);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to write run report for {System}", result.System);
        }
    }

    private static string BuildMarkdown(Result result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {result.System} ({result.City})");
        sb.AppendLine();
        sb.AppendLine($"Run at {result.RanAtUtc:yyyy-MM-dd HH:mm} UTC — {(result.Succeeded ? "succeeded" : "FAILED")}");
        sb.AppendLine();

        if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.Error))
        {
            sb.AppendLine($"> **Error:** {result.Error}");
            sb.AppendLine();
        }

        var counts = result.Counts.Where(c => c.Value != 0).ToList();
        if (counts.Count > 0)
        {
            sb.AppendLine("| Change | Count |");
            sb.AppendLine("|---|---:|");
            foreach (var (label, value) in counts)
            {
                sb.AppendLine($"| {label} | {value} |");
            }
            sb.AppendLine();
        }

        if (result.Checklist.Count > 0)
        {
            sb.AppendLine("## Next steps");
            sb.AppendLine();
            foreach (var item in result.Checklist)
            {
                sb.AppendLine($"- [ ] {item}");
            }
        }
        else if (result.Succeeded)
        {
            sb.AppendLine("Nothing to do — OSM and GBFS agree.");
        }

        return sb.ToString();
    }
}
