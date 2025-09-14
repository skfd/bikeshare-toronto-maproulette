using System.Diagnostics;
using Serilog;

namespace prepareBikeParking
{
    public static class GitFunctions
    {
        public static DateTime? GetLastCommitDateForFile(string filePath)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"log -1 --format=%ci -- \"{filePath.Replace('\\', '/')}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    Log.Error("Failed to start git process for file {FilePath}", filePath);
                    return null;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (DateTime.TryParse(output.Trim(), out var commitDate))
                {
                    return commitDate;
                }
                else
                {
                    Log.Warning("Failed to parse commit date for file {FilePath}. Raw output: {Output}", filePath, output.Trim());
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while getting last commit date for file {FilePath}", filePath);
                return null;
            }
        }
    }
}
