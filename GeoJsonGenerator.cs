namespace prepareBikeParking
{
    public static class GeoJsonGenerator
    {
        public static async Task GenerateMainFileAsync(List<GeoPoint> locationsList, string systemName)
        {
            Console.WriteLine("Generating main geojson file...");
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare.geojson", locationsList, point => GenerateGeojsonLine(point, systemName));
            Console.WriteLine("Main geojson file saved.");
        }

        public static async Task GenerateDiffFilesAsync(List<GeoPoint> addedPoints, List<GeoPoint> removedPoints, List<GeoPoint> movedPoints, List<(GeoPoint current, GeoPoint old)> renamedPoints, string systemName)
        {
            Console.WriteLine("Generating diff files...");

            await FileManager.WriteSystemGeoJsonFileWithOldNamesAsync(systemName, "bikeshare_renamed.geojson", renamedPoints, (point, oldName) => GenerateGeojsonLineWithOldName(point, oldName, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_added.geojson", addedPoints, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_toreview.geojson", addedPoints, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_removed.geojson", removedPoints, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_moved.geojson", movedPoints, point => GenerateGeojsonLine(point, systemName));

            Console.WriteLine("Diff files generated successfully.");
        }

        public static string GenerateGeojsonLine(GeoPoint point, string systemName)
        {
            var template = "\u001e{{\"type\":\"FeatureCollection\"" +
                ",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"Point\"," +
                "\"coordinates\":[{0},{1}]}},\"properties\":{{" +
                        "\"address\":\"{2}\"," +
                        "\"latitude\":\"{1}\"," +
                        "\"longitude\":\"{0}\"," +
                        "\"name\":\"{3}\"," +
                        "\"capacity\":\"{4}\"," +
                "\"operator\":\"{5}\"}}}}]}}";

            return string.Format(
                template,
                GeoPoint.ParseCoords(point.lon).ToString(System.Globalization.CultureInfo.InvariantCulture),
                GeoPoint.ParseCoords(point.lat).ToString(System.Globalization.CultureInfo.InvariantCulture),
                point.id,
                point.name.Trim(),
                point.capacity,
                systemName);
        }

        public static string GenerateGeojsonLineWithOldName(GeoPoint point, string oldName, string systemName)
        {
            var template = "\u001e{{\"type\":\"FeatureCollection\"" +
                ",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"Point\"," +
                "\"coordinates\":[{0},{1}]}},\"properties\":{{" +
                        "\"address\":\"{2}\"," +
                        "\"latitude\":\"{1}\"," +
                        "\"longitude\":\"{0}\"," +
                        "\"name\":\"{3}\"," +
                        "\"oldName\":\"{6}\"," +
                        "\"capacity\":\"{4}\"," +
                "\"operator\":\"{5}\"}}}}]}}";

            return string.Format(
                template,
                GeoPoint.ParseCoords(point.lon).ToString(System.Globalization.CultureInfo.InvariantCulture),
                GeoPoint.ParseCoords(point.lat).ToString(System.Globalization.CultureInfo.InvariantCulture),
                point.id,
                point.name.Trim(),
                point.capacity,
                systemName,
                oldName.Trim());
        }

        public static async Task GenerateOSMComparisonFilesAsync(List<GeoPoint> missingInOSM, List<GeoPoint> extraInOSM, List<GeoPoint> differentInOSM, List<(GeoPoint current, GeoPoint old)> renamedInOSM, string systemName)
        {
            Console.WriteLine("Generating OSM comparison files...");

            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_missing_in_osm.geojson", missingInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_extra_in_osm.geojson", extraInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_moved_in_osm.geojson", differentInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileWithOldNamesAsync(systemName, "bikeshare_renamed_in_osm.geojson", renamedInOSM, (point, oldName) => GenerateGeojsonLineWithOldName(point, oldName, systemName));

            Console.WriteLine("OSM comparison files generated successfully.");
        }
    }
}
