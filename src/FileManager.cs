using System.Text;
using System.Text.Json;

namespace prepareBikeParking
{
    /// <summary>
    /// Centralized file handling class for reading and writing various file types used in the application
    /// </summary>
    public static class FileManager
    {
        /// <summary>
        /// Data results directory path
        /// </summary>
        private const string DataResultsPath = "data_results";

        /// <summary>
        /// Gets the base path for file operations, handling different working directories
        /// </summary>
        private static string GetBasePath()
        {
            // Resolution strategy (ordered):
            // 1. Current directory (running from src)
            // 2. src/ relative to current directory (running from project root)
            // 3. ../../../../src (running from bin/Debug/netX.Y)
            // 4. ../../../../ (fallback if data_results later moved to root)
            var possibleBasePaths = new[]
            {
                "",                    // from src directory
                "src\\",              // from project root
                "..\\..\\..\\src\\", // from bin output directory back to src
                "..\\..\\..\\"        // fallback root (future-proof if layout changes)
            };

            foreach (var basePath in possibleBasePaths)
            {
                var testPath = Path.Combine(basePath, DataResultsPath);
                if (Directory.Exists(testPath))
                {
                    return basePath;
                }
            }

            // As a last resort create data_results in current directory (should rarely happen)
            return "";
        }

        #region Path Utilities

        /// <summary>
        /// Gets the full path for a file in a system-specific directory
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <returns>Full path to the file</returns>
        public static string GetSystemFilePath(string systemName, string fileName)
        {
            // Comprehensive sanitization: replace path separators and remove path traversal sequences
            var safeSystemName = systemName.Replace('/', '_')
                                           .Replace('\\', '_')
                                           .Replace(':', '_')           // Remove colon to prevent drive letters
                                           .Replace("..", string.Empty) // Remove path traversal
                                           .Replace(".", string.Empty)  // Remove all remaining dots to prevent hidden path traversal
                                           .Trim();                      // Remove leading/trailing whitespace

            // Ensure we don't have an empty system name after sanitization
            if (string.IsNullOrWhiteSpace(safeSystemName))
            {
                safeSystemName = "unnamed_system";
            }

            return Path.Combine(DataResultsPath, safeSystemName, fileName);
        }

        #endregion

        #region Text File Operations

        /// <summary>
        /// Reads text content from a file
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>File content as string</returns>
        public static async Task<string> ReadTextFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(GetBasePath(), relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            return await File.ReadAllTextAsync(fullPath, Encoding.UTF8);
        }        /// <summary>
        /// Reads text content from a system-specific file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <returns>File content as string</returns>
        public static async Task<string> ReadSystemTextFileAsync(string systemName, string fileName)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            return await ReadTextFileAsync(relativePath);
        }

        /// <summary>
        /// Reads text content from a file synchronously
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>File content as string</returns>
        public static string ReadTextFile(string relativePath)
        {
            var fullPath = Path.Combine(GetBasePath(), relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            return File.ReadAllText(fullPath, Encoding.UTF8);
        }

        /// <summary>
        /// Writes text content to a file
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <param name="content">Content to write</param>
        public static async Task WriteTextFileAsync(string relativePath, string content)
        {
            var fullPath = Path.Combine(GetBasePath(), relativePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
        }

        /// <summary>
        /// Writes text content to a system-specific file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <param name="content">Content to write</param>
        public static async Task WriteSystemTextFileAsync(string systemName, string fileName, string content)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            await WriteTextFileAsync(relativePath, content);
        }

        /// <summary>
        /// Writes text content to a file synchronously
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <param name="content">Content to write</param>
        public static void WriteTextFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(GetBasePath(), relativePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content, Encoding.UTF8);
        }

        #endregion

        #region JSON File Operations

        /// <summary>
        /// Reads and deserializes JSON content from a file
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>Deserialized object</returns>
        public static async Task<T> ReadJsonFileAsync<T>(string relativePath) where T : class
        {
            var jsonContent = await ReadTextFileAsync(relativePath);
            var result = JsonSerializer.Deserialize<T>(jsonContent);

            if (result == null)
            {
                throw new InvalidOperationException($"Failed to parse JSON file '{relativePath}'. The file may contain invalid JSON or result in null.");
            }

            return result;
        }

        /// <summary>
        /// Serializes and writes an object to a JSON file
        /// </summary>
        /// <typeparam name="T">Type of object to serialize</typeparam>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <param name="obj">Object to serialize</param>
        /// <param name="prettyPrint">Whether to format JSON with indentation</param>
        public static async Task WriteJsonFileAsync<T>(string relativePath, T obj, bool prettyPrint = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = prettyPrint,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonContent = JsonSerializer.Serialize(obj, options);
            await WriteTextFileAsync(relativePath, jsonContent);
        }

        #endregion

        #region Line-based File Operations

        /// <summary>
        /// Reads file content and splits into lines
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <param name="removeEmptyLines">Whether to remove empty lines</param>
        /// <returns>Array of lines</returns>
        public static async Task<string[]> ReadLinesAsync(string relativePath, bool removeEmptyLines = true)
        {
            var content = await ReadTextFileAsync(relativePath);
            var splitOptions = removeEmptyLines ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
            return content.Split('\n', splitOptions);
        }

        /// <summary>
        /// Writes an enumerable of strings as lines to a file
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <param name="lines">Lines to write</param>
        public static async Task WriteLinesAsync(string relativePath, IEnumerable<string> lines)
        {
            var content = string.Join("\n", lines);
            await WriteTextFileAsync(relativePath, content);
        }

        /// <summary>
        /// Writes an enumerable of strings as lines to a system-specific file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <param name="lines">Lines to write</param>
        public static async Task WriteSystemLinesAsync(string systemName, string fileName, IEnumerable<string> lines)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            await WriteLinesAsync(relativePath, lines);
        }

        #endregion

        #region File Existence and Utilities

        /// <summary>
        /// Checks if a file exists
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>True if file exists</returns>
        public static bool FileExists(string relativePath)
        {
            var fullPath = Path.Combine(GetBasePath(), relativePath);
            return File.Exists(fullPath);
        }

        /// <summary>
        /// Checks if a system-specific file exists
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <returns>True if file exists</returns>
        public static bool SystemFileExists(string systemName, string fileName)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            return FileExists(relativePath);
        }

        /// <summary>
        /// Gets the full path for a relative path
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>Full path</returns>
        public static string GetFullPath(string relativePath)
        {
            var combinedPath = Path.Combine(GetBasePath(), relativePath);
            // Normalize the path to resolve any ".." sequences
            return Path.GetFullPath(combinedPath);
        }

        /// <summary>
        /// Gets the full path for a system-specific file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <returns>Full path</returns>
        public static string GetSystemFullPath(string systemName, string fileName)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            return GetFullPath(relativePath);
        }

        /// <summary>
        /// Deletes a file if it exists
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>True if file was deleted, false if it didn't exist</returns>
        public static bool DeleteFile(string relativePath)
        {
            var fullPath = Path.Combine(GetBasePath(), relativePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets file information
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>FileInfo object or null if file doesn't exist</returns>
        public static FileInfo? GetFileInfo(string relativePath)
        {
            var fullPath = Path.Combine(GetBasePath(), relativePath);

            if (File.Exists(fullPath))
            {
                return new FileInfo(fullPath);
            }

            return null;
        }

        #endregion

        #region Specialized File Operations

        /// <summary>
        /// Reads a GeoJSON file and parses it into GeoPoint objects
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <returns>List of GeoPoint objects</returns>
        public static async Task<List<GeoPoint>> ReadGeoJsonFileAsync(string relativePath)
        {
            var lines = await ReadLinesAsync(relativePath, removeEmptyLines: true);
            return lines.Select(GeoPoint.ParseLine).ToList();
        }

        /// <summary>
        /// Reads a system-specific GeoJSON file and parses it into GeoPoint objects
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <returns>List of GeoPoint objects</returns>
        public static async Task<List<GeoPoint>> ReadSystemGeoJsonFileAsync(string systemName, string fileName)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            return await ReadGeoJsonFileAsync(relativePath);
        }

        /// <summary>
        /// Writes GeoPoint objects to a GeoJSON file
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <param name="geoPoints">GeoPoint objects to write</param>
        /// <param name="generateLineFunc">Function to generate GeoJSON line from GeoPoint</param>
        public static async Task WriteGeoJsonFileAsync(string relativePath, IEnumerable<GeoPoint> geoPoints, Func<GeoPoint, string> generateLineFunc)
        {
            var lines = geoPoints.OrderBy(x => x.id).Select(generateLineFunc);
            await WriteLinesAsync(relativePath, lines);
        }

        /// <summary>
        /// Writes GeoPoint objects to a system-specific GeoJSON file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <param name="geoPoints">GeoPoint objects to write</param>
        /// <param name="generateLineFunc">Function to generate GeoJSON line from GeoPoint</param>
        public static async Task WriteSystemGeoJsonFileAsync(string systemName, string fileName, IEnumerable<GeoPoint> geoPoints, Func<GeoPoint, string> generateLineFunc)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            await WriteGeoJsonFileAsync(relativePath, geoPoints, generateLineFunc);
        }

        /// <summary>
        /// Writes GeoPoint objects with old names to a GeoJSON file
        /// </summary>
        /// <param name="relativePath">Path relative to the base directory</param>
        /// <param name="geoPointPairs">Tuples of current and old GeoPoint objects</param>
        /// <param name="generateLineFunc">Function to generate GeoJSON line from GeoPoint pair</param>
        public static async Task WriteGeoJsonFileWithOldNamesAsync(string relativePath, IEnumerable<(GeoPoint current, GeoPoint old)> geoPointPairs, Func<GeoPoint, string, string> generateLineFunc)
        {
            var lines = geoPointPairs.OrderBy(x => x.current.id).Select(x => generateLineFunc(x.current, x.old.name));
            await WriteLinesAsync(relativePath, lines);
        }

        /// <summary>
        /// Writes GeoPoint objects with old names to a system-specific GeoJSON file
        /// </summary>
        /// <param name="systemName">Name of the bike share system</param>
        /// <param name="fileName">Name of the file</param>
        /// <param name="geoPointPairs">Tuples of current and old GeoPoint objects</param>
        /// <param name="generateLineFunc">Function to generate GeoJSON line from GeoPoint pair</param>
        public static async Task WriteSystemGeoJsonFileWithOldNamesAsync(string systemName, string fileName, IEnumerable<(GeoPoint current, GeoPoint old)> geoPointPairs, Func<GeoPoint, string, string> generateLineFunc)
        {
            var relativePath = GetSystemFilePath(systemName, fileName);
            await WriteGeoJsonFileWithOldNamesAsync(relativePath, geoPointPairs, generateLineFunc);
        }

        #endregion
    }
}
