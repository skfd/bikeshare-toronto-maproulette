using System.Diagnostics;
using Serilog;

namespace prepareBikeParking;

/// <summary>
/// Commits the refreshed GeoJSON as the next baseline.
///
/// The tool reads the previous snapshot with `git show HEAD:...`, so the diff is
/// only meaningful if somebody committed the last run's output. Leaving that to
/// the operator was the easiest step in the whole loop to forget, and forgetting
/// it silently corrupts the next run's "what changed" numbers.
///
/// Committing early does not consume pending MapRoulette work: missing_in_osm and
/// the renames come from the GBFS-vs-OSM comparison, not from the git diff.
/// </summary>
public static class BaselineCommitter
{
    /// <summary>
    /// Set when --commit-baseline is in play, so the operator checklist does not
    /// ask for a commit that this run is about to make itself.
    /// </summary>
    public static bool Enabled { get; set; }

    public static async Task CommitAsync(IReadOnlyList<int> systemIds)
    {
        var names = new List<string>();
        foreach (var id in systemIds)
        {
            try
            {
                var system = await BikeShareSystemLoader.LoadSystemByIdAsync(id);
                names.Add(system.Name);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not resolve system {Id} while committing baseline", id);
            }
        }

        if (names.Count == 0) return;

        var paths = names
            .Select(n => Path.GetDirectoryName(FileManager.GetSystemFullPath(n, "x")))
            .Where(d => d != null && Directory.Exists(d))
            .Select(d => d!.Replace('\\', '/'))
            .ToList();

        if (paths.Count == 0)
        {
            Log.Information("No data directories to commit.");
            return;
        }

        if (Run("add", string.Join(" ", paths.Select(p => $"\"{p}\""))) != 0)
        {
            ConsoleUI.PrintWarning("git add failed; baseline not committed.");
            return;
        }

        // Nothing staged means nothing changed - that is a normal quiet week.
        if (Run("diff", "--cached --quiet") == 0)
        {
            Log.Information("No baseline changes to commit.");
            ConsoleUI.PrintInfo("No baseline changes to commit.");
            return;
        }

        var subject = names.Count == 1
            ? $"{names[0]} data refresh"
            : $"Data refresh: {string.Join(", ", names)}";

        if (Run("commit", $"-m \"{subject}\"") != 0)
        {
            ConsoleUI.PrintWarning("git commit failed; baseline not committed.");
            return;
        }

        Log.Information("Committed baseline for {Systems}", string.Join(", ", names));
        ConsoleUI.PrintSuccess($"Committed baseline: {subject}");
    }

    private static int Run(string verb, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"{verb} {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null)
        {
            Log.Error("Failed to start git {Verb}", verb);
            return -1;
        }

        var stderr = proc.StandardError.ReadToEnd();
        proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            Log.Warning("git {Verb} exited {Code}: {Error}", verb, proc.ExitCode, stderr.Trim());
        }

        return proc.ExitCode;
    }
}
