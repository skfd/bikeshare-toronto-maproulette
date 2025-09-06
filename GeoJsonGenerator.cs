namespace prepareBikeParking
{
    public static class GeoJsonGenerator
    {
        public static async Task GenerateMainFileAsync(List<GeoPoint> locationsList)
        {
            Console.WriteLine("Generating main geojson file...");
            await FileManager.WriteGeoJsonFileAsync("bikeshare.geojson", locationsList, GenerateGeojsonLine);
            Console.WriteLine("Main geojson file saved.");
        }

        public static async Task GenerateDiffFilesAsync(List<GeoPoint> addedPoints, List<GeoPoint> removedPoints, List<GeoPoint> movedPoints, List<(GeoPoint current, GeoPoint old)> renamedPoints)
        {
            Console.WriteLine("Generating diff files...");

            await FileManager.WriteGeoJsonFileWithOldNamesAsync("bikeshare_renamed.geojson", renamedPoints, GenerateGeojsonLineWithOldName);
            await FileManager.WriteGeoJsonFileAsync("bikeshare_added.geojson", addedPoints, GenerateGeojsonLine);
            await FileManager.WriteGeoJsonFileAsync("bikeshare_toreview.geojson", addedPoints, GenerateGeojsonLine);
            await FileManager.WriteGeoJsonFileAsync("bikeshare_removed.geojson", removedPoints, GenerateGeojsonLine);
            await FileManager.WriteGeoJsonFileAsync("bikeshare_moved.geojson", movedPoints, GenerateGeojsonLine);

            Console.WriteLine("Diff files generated successfully.");
        }

        public static string GenerateGeojsonLine(GeoPoint point)
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
                "BikeShare Toronto");
        }

        public static string GenerateGeojsonLineWithOldName(GeoPoint point, string oldName)
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
                "BikeShare Toronto",
                oldName.Trim());
        }

        public static async Task GenerateOSMComparisonFilesAsync(List<GeoPoint> missingInOSM, List<GeoPoint> extraInOSM, List<GeoPoint> differentInOSM, List<(GeoPoint current, GeoPoint old)> renamedInOSM)
        {
            Console.WriteLine("Generating OSM comparison files...");

            await FileManager.WriteGeoJsonFileAsync("bikeshare_missing_in_osm.geojson", missingInOSM, GenerateGeojsonLine);
            await FileManager.WriteGeoJsonFileAsync("bikeshare_extra_in_osm.geojson", extraInOSM, GenerateGeojsonLine);
            await FileManager.WriteGeoJsonFileAsync("bikeshare_moved_in_osm.geojson", differentInOSM, GenerateGeojsonLine);
            await FileManager.WriteGeoJsonFileWithOldNamesAsync("bikeshare_renamed_in_osm.geojson", renamedInOSM, GenerateGeojsonLineWithOldName);

            Console.WriteLine("OSM comparison files generated successfully.");
        }
    }
}
