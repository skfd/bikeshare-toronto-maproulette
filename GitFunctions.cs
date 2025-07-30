using System.Diagnostics;

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
                    Arguments = $"log -1 --format=%ci -- {filePath}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    Console.WriteLine("Failed to start git process.");
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
                    Console.WriteLine("Failed to parse commit date.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while getting last commit date: {ex.Message}");
                return null;
            }
        }
    }
}
