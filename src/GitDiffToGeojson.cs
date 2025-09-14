using System.Diagnostics;
using System.Text;
using Serilog;

namespace prepareBikeParking
{
    public static class GitDiffToGeojson
    {
        public static (List<string>, List<string>) LatestVsPrevious(string? filePath = null)
        {
            var diffOutput = RunGitDiffCommand("0", "", filePath);
            var (addedlines, removedObjects) = ExtractDiffedLines(diffOutput);

            return (addedlines, removedObjects);
        }

        public static string GetLastCommittedVersion(string filePath)
        {
            string command = $"show HEAD:\"{filePath.Replace('\\','/')}\"";
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
                        var errorMessage = errorOutput.ToString();
                        // Check if it's a "file not found in git" error
                        if (errorMessage.Contains("does not exist") || errorMessage.Contains("Path") || errorMessage.Contains("fatal"))
                        {
                            throw new FileNotFoundException($"File '{filePath}' not found in git repository. This might be a new system.", filePath);
                        }
                        throw new Exception($"Git command failed: {errorMessage}");
                    }
                }
            }

            return output.ToString();
        }

        public static (List<string>, List<string>) Compare(string @new = "HEAD", string old = "", string? filePath = null)
        {
            var diffOutput = RunGitDiffCommand(@new, old, filePath);
            var (addedlines, removedObjects) = ExtractDiffedLines(diffOutput);

            return (addedlines, removedObjects);
        }

        private static string RunGitDiffCommand(string @new = "HEAD", string old = "", string? filePath = null)
        {
            // Use the provided file path or fallback to the old hardcoded path for backward compatibility
            var targetFile = filePath ?? "../../../bikeshare.geojson";
            string command = $"--no-pager diff --unified=0 {@new} {old} \"{targetFile.Replace('\\', '/')}\"";
            string arguments = "";

            Log.Debug("Running git diff command: git {Command} {Args}", command, arguments);

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

                var stdOut = output.ToString();
                var stdErr = errorOutput.ToString();
                if (!string.IsNullOrWhiteSpace(stdOut))
                {
                    Log.Debug("git diff stdout for {File}: {StdOut}", targetFile, stdOut);
                }
                if (!string.IsNullOrWhiteSpace(stdErr))
                {
                    Log.Debug("git diff stderr for {File}: {StdErr}", targetFile, stdErr);
                }
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
