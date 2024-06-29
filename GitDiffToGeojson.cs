using System;
using System.Diagnostics;
using System.Text;

namespace prepareBikeParking
{
    public static class GitDiffToGeojson
    {
        public static List<string> LatestVsPrevious()
        {
            var diffOutput = RunGitDiffCommand();
            var (addedlines, removedObjects) = ExtractDiffedLines(diffOutput);

            return addedlines;
        }

        private static string RunGitDiffCommand()
        {
            string command = "--no-pager diff --unified=0 3a71a411";
            string arguments = "";

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
            /*
            diff --git a/bikeshare.geojson b/bikeshare.geojson
            index 8811f3f..2065df5 100644
            --- a/bikeshare.geojson
            +++ b/bikeshare.geojson
            @@ -88 +87,0 @@
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.35347,43.667819]},"properties":{"address":"7098","latitude":"43.667819","longitude":"-79.35347","operator":"BikeShare Toronto"}}]}
            @@ -582 +581 @@
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.403341,43.635007]},"properties":{"address":"7707","latitude":"43.635007","longitude":"-79.403341","operator":"BikeShare Toronto"}}]}
            +{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.4035221,43.635495]},"properties":{"address":"7707","latitude":"43.635495","longitude":"-79.4035221","operator":"BikeShare Toronto"}}]}
            @@ -660 +659 @@
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.382668961517,43.669337508936]},"properties":{"address":"7795","latitude":"43.669337508936","longitude":"-79.382668961517","operator":"BikeShare Toronto"}}]}
            +{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.381145747568,43.666850777485]},"properties":{"address":"7795","latitude":"43.666850777485","longitude":"-79.381145747568","operator":"BikeShare Toronto"}}]}
            @@ -800,6 +799 @@
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.408973218866,43.63467323676]},"properties":{"address":"7939","latitude":"43.63467323676","longitude":"-79.408973218866","operator":"BikeShare Toronto"}}]}
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.393561616533,43.706072872485]},"properties":{"address":"7940","latitude":"43.706072872485","longitude":"-79.393561616533","operator":"BikeShare Toronto"}}]}
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.420022,43.639547]},"properties":{"address":"7941","latitude":"43.639547","longitude":"-79.420022","operator":"BikeShare Toronto"}}]}
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.389027108127,43.705121266154]},"properties":{"address":"7942","latitude":"43.705121266154","longitude":"-79.389027108127","operator":"BikeShare Toronto"}}]}
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.390911850002,43.652315523048]},"properties":{"address":"7943","latitude":"43.652315523048","longitude":"-79.390911850002","operator":"BikeShare Toronto"}}]}
            -{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.417873888886,43.659953635181]},"properties":{"address":"7944","latitude":"43.659953635181","longitude":"-79.417873888886","operator":"BikeShare Toronto"}}]}
            \ No newline at end of file
            +{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-79.408973218866,43.63467323676]},"properties":{"address":"7939","latitude":"43.63467323676","longitude":"-79.408973218866","operator":"BikeShare Toronto"}}]}
            \ No newline at end of file
            */

            var addedObjects = new List<string>();
            var removedObjects = new List<string>();

            var lines = gitDiffInput.Split("\n");
            lines
                .Where(x =>
                    x.StartsWith("-\u001e{") || 
                    x.StartsWith("+\u001e{"))
                .ToList()
                .ForEach(x =>
                {
                    if (x.StartsWith("-\u001e"))
                    {
                        removedObjects.Add(x.Replace("-\u001e", ""));
                    }
                    if (x.StartsWith("+\u001e"))
                    {
                        addedObjects.Add(x.Replace("+\u001e", ""));
                    }
                });

            return (addedObjects, removedObjects);
        }
    }
}
