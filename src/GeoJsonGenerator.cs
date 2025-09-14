using Serilog;

namespace prepareBikeParking
{
    public static class GeoJsonGenerator
    {
        public static async Task GenerateMainFileAsync(List<GeoPoint> locationsList, string systemName)
        {
            Log.Information("Generating main geojson file for {SystemName} with {Count} points", systemName, locationsList.Count);
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare.geojson", locationsList, point => GenerateGeojsonLine(point, systemName));
            Log.Information("Main geojson file saved for {SystemName}", systemName);
        }

        public static async Task GenerateDiffFilesAsync(List<GeoPoint> addedPoints, List<GeoPoint> removedPoints, List<GeoPoint> movedPoints, List<(GeoPoint current, GeoPoint old)> renamedPoints, string systemName)
        {
            Log.Information("Generating diff files for {SystemName}: Added={Added} Removed={Removed} Moved={Moved} Renamed={Renamed}", systemName, addedPoints.Count, removedPoints.Count, movedPoints.Count, renamedPoints.Count);

            await FileManager.WriteSystemGeoJsonFileWithOldNamesAsync(systemName, "bikeshare_renamed.geojson", renamedPoints, (point, oldName) => GenerateGeojsonLineWithOldName(point, oldName, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_added.geojson", addedPoints, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_toreview.geojson", addedPoints, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_removed.geojson", removedPoints, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_moved.geojson", movedPoints, point => GenerateGeojsonLine(point, systemName));

            Log.Information("Diff files generated successfully for {SystemName}", systemName);
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
            Log.Information("Generating OSM comparison files for {SystemName}: Missing={Missing} Extra={Extra} Moved={Moved} Renamed={Renamed}", systemName, missingInOSM.Count, extraInOSM.Count, differentInOSM.Count, renamedInOSM.Count);

            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_missing_in_osm.geojson", missingInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_extra_in_osm.geojson", extraInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_moved_in_osm.geojson", differentInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileWithOldNamesAsync(systemName, "bikeshare_renamed_in_osm.geojson", renamedInOSM, (point, oldName) => GenerateGeojsonLineWithOldName(point, oldName, systemName));

            Log.Information("OSM comparison files generated successfully for {SystemName}", systemName);
        }
    }
}
