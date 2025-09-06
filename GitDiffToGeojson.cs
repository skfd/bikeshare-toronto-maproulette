using System.Diagnostics;
using System.Text;

namespace prepareBikeParking
{
    public static class GitDiffToGeojson
    {
        public static (List<string>, List<string>) LatestVsPrevious()
        {
            var diffOutput = RunGitDiffCommand("0");
            var (addedlines, removedObjects) = ExtractDiffedLines(diffOutput);

            return (addedlines, removedObjects);
        }

        public static string GetLastCommittedVersion()
        {
            string command = "show HEAD:../../../bikeshare.geojson";
            string arguments = "";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"{command} {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, // Ensure UTF-8 encoding
                StandardErrorEncoding = Encoding.UTF8 // Ensure UTF-8 encoding for error output
            };

            StringBuilder output = new StringBuilder();
            StringBuilder errorOutput = new StringBuilder();

            using (Process process = Process.Start(startInfo))
            {
                if (process != null)
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        output.AppendLine(process.StandardOutput.ReadLine());
                    }

                    while (!process.StandardError.EndOfStream)
                    {
                        errorOutput.AppendLine(process.StandardError.ReadLine());
                    }

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Git command failed: {errorOutput}");
                    }
                }
            }

            return output.ToString();
        }

        public static (List<string>, List<string>) Compare(string @new = "HEAD", string old = "")
        {
            var diffOutput = RunGitDiffCommand(@new, old);
            var (addedlines, removedObjects) = ExtractDiffedLines(diffOutput);

            return (addedlines, removedObjects);
        }

        private static string RunGitDiffCommand(string @new = "HEAD", string old = "")
        {
            string command = $"--no-pager diff --unified=0 {@new} {old} \"../../../bikeshare.geojson\"";
            string arguments = "";

            Console.WriteLine($"Running git {command} {arguments}");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"{command} {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Add this line to redirect the error stream
                UseShellExecute = false,
                CreateNoWindow = true
            };

            StringBuilder output = new StringBuilder();
            StringBuilder errorOutput = new StringBuilder(); // Create a StringBuilder to store the error output

            // Start the process
            using Process process = Process.Start(startInfo);
            if (process != null)
            {
                // Read the output
                while (!process.StandardOutput.EndOfStream)
                {
                    output.AppendLine(process.StandardOutput.ReadLine());
                }

                // Read the error output
                while (!process.StandardError.EndOfStream)
                {
                    errorOutput.AppendLine(process.StandardError.ReadLine());
                }

                // Wait for the process to finish
                process.WaitForExit();

                // Output the result
                Console.WriteLine(output.ToString());

                // Output the error result
                Console.WriteLine(errorOutput.ToString());
            }

            var result = output.ToString();

            return result;
        }

        private static (List<string>, List<string>) ExtractDiffedLines(string gitDiffInput)
        {
            var lines = gitDiffInput.Split("\n");

            var addedObjects =
                lines
                .Where(x =>
                    x.StartsWith("+\u001e{"))
                .Select(x =>
                    x.Replace("+\u001e{", "{"))
                .ToList();

            var removedObjects =
                lines
                .Where(x =>
                    x.StartsWith("-\u001e{"))
                .Select(x =>
                    x.Replace("-\u001e{", "{"))
                .ToList();

            return (addedObjects, removedObjects);
        }
    }
}
