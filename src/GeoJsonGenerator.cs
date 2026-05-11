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
            var template = "{{\"type\":\"FeatureCollection\"" +
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
            var template = "{{\"type\":\"FeatureCollection\"" +
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

        public static string GenerateGeojsonLineWithError(GeoPoint point, string systemName, string errorMessage)
        {
            var template = "{{\"type\":\"FeatureCollection\"" +
                ",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"Point\"," +
                "\"coordinates\":[{0},{1}]}},\"properties\":{{" +
                        "\"address\":\"{2}\"," +
                        "\"latitude\":\"{1}\"," +
                        "\"longitude\":\"{0}\"," +
                        "\"name\":\"{3}\"," +
                        "\"capacity\":\"{4}\"," +
                        "\"operator\":\"{5}\"," +
                        "\"error\":\"{6}\"," +
                        "\"osmType\":\"{7}\"," +
                        "\"osmId\":\"{8}\"}}}}]}}";

            return string.Format(
                template,
                GeoPoint.ParseCoords(point.lon).ToString(System.Globalization.CultureInfo.InvariantCulture),
                GeoPoint.ParseCoords(point.lat).ToString(System.Globalization.CultureInfo.InvariantCulture),
                point.id,
                point.name.Trim(),
                point.capacity,
                systemName,
                errorMessage.Replace("\"", "\\\""),
                point.osmType ?? "",
                point.osmId ?? "");
        }

        public static async Task GenerateOSMComparisonFilesAsync(List<GeoPoint> missingInOSM, List<GeoPoint> extraInOSM, List<GeoPoint> differentInOSM, List<(GeoPoint current, GeoPoint old)> renamedInOSM, List<GeoPoint> closedStations, string systemName)
        {
            Log.Information("Generating OSM comparison files for {SystemName}: Missing={Missing} Extra={Extra} Moved={Moved} Renamed={Renamed} Closed={Closed}", systemName, missingInOSM.Count, extraInOSM.Count, differentInOSM.Count, renamedInOSM.Count, closedStations.Count);

            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_missing_in_osm.geojson", missingInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_extra_in_osm.geojson", extraInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_moved_in_osm.geojson", differentInOSM, point => GenerateGeojsonLine(point, systemName));
            await FileManager.WriteSystemGeoJsonFileWithOldNamesAsync(systemName, "bikeshare_renamed_in_osm.geojson", renamedInOSM, (point, oldName) => GenerateGeojsonLineWithOldName(point, oldName, systemName));
            await FileManager.WriteSystemGeoJsonFileAsync(systemName, "bikeshare_closed.geojson", closedStations, point => GenerateGeojsonLine(point, systemName));

            Log.Information("OSM comparison files generated successfully for {SystemName}", systemName);
        }

        public static async Task GenerateRefConflictsFileAsync(IReadOnlyList<RefConflictDetector.RefConflict> conflicts, string systemName)
        {
            var fileName = "bikeshare_ref_conflicts.geojson";
            if (conflicts.Count == 0)
            {
                var stale = FileManager.GetSystemFullPath(systemName, fileName);
                if (File.Exists(stale)) File.Delete(stale);
                return;
            }

            Log.Information("Generating ref-conflict file for {SystemName}: {Count} conflict(s)", systemName, conflicts.Count);
            var lines = conflicts
                .OrderBy(c => c.Kind)
                .ThenBy(c => c.OsmNode.osmId)
                .Select(c => GenerateGeojsonLineForRefConflict(c, systemName));
            await FileManager.WriteSystemLinesAsync(systemName, fileName, lines);
        }

        public static string GenerateGeojsonLineForRefConflict(RefConflictDetector.RefConflict c, string systemName)
        {
            static string J(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            static string Num(double d) => Math.Round(d).ToString(System.Globalization.CultureInfo.InvariantCulture);

            var osm = c.OsmNode;
            var lon = GeoPoint.ParseCoords(osm.lon).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lat = GeoPoint.ParseCoords(osm.lat).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var address = c.Kind == "fix-ref" ? (c.ResolvedGbfs?.id ?? osm.id) : osm.id;

            var props = new System.Text.StringBuilder();
            props.Append($"\"address\":\"{J(address)}\",");
            props.Append($"\"latitude\":\"{lat}\",");
            props.Append($"\"longitude\":\"{lon}\",");
            props.Append($"\"name\":\"{J(osm.name.Trim())}\",");
            props.Append($"\"capacity\":\"{osm.capacity}\",");
            props.Append($"\"operator\":\"{J(systemName)}\",");
            props.Append($"\"osmType\":\"{J(osm.osmType ?? "")}\",");
            props.Append($"\"osmId\":\"{J(osm.osmId ?? "")}\",");
            props.Append($"\"action\":\"{c.Kind}\",");
            props.Append($"\"currentRef\":\"{J(c.CurrentRef ?? "")}\"");

            if (c.Kind == "fix-ref" && c.ResolvedGbfs != null)
            {
                props.Append($",\"resolvedRef\":\"{J(c.ResolvedGbfs.id)}\"");
                if (c.ResolvedDistanceMeters is double rd) props.Append($",\"resolvedDistanceMeters\":\"{Num(rd)}\"");
            }
            else
            {
                if (c.ClaimedGbfsName != null) props.Append($",\"claimedGbfsName\":\"{J(c.ClaimedGbfsName.Trim())}\"");
                if (c.ClaimedDistanceMeters is double cd) props.Append($",\"claimedDistanceMeters\":\"{Num(cd)}\"");
                if (c.ResolvedGbfs != null)
                {
                    props.Append($",\"likelyRef\":\"{J(c.ResolvedGbfs.id)}\"");
                    props.Append($",\"likelyName\":\"{J(c.ResolvedGbfs.name.Trim())}\"");
                    if (c.ResolvedDistanceMeters is double rd) props.Append($",\"likelyDistanceMeters\":\"{Num(rd)}\"");
                }
            }

            return "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\",\"coordinates\":["
                + lon + "," + lat + "]},\"properties\":{" + props + "}}]}";
        }

        public static async Task GenerateReactivationsFileAsync(List<(GeoPoint current, GeoPoint disused)> reactivations, string systemName)
        {
            Log.Information("Generating reactivation file for {SystemName}: {Count} station(s)", systemName, reactivations.Count);

            var lines = reactivations
                .OrderBy(r => r.current.id)
                .Select(r => GenerateGeojsonLineForReactivation(r.current, r.disused, systemName));

            await FileManager.WriteSystemLinesAsync(systemName, "bikeshare_reactivated_in_osm.geojson", lines);

            Log.Information("Reactivation file generated successfully for {SystemName}", systemName);
        }

        public static string GenerateGeojsonLineForReactivation(GeoPoint current, GeoPoint disused, string systemName)
        {
            var template = "{{\"type\":\"FeatureCollection\"" +
                ",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"Point\"," +
                "\"coordinates\":[{0},{1}]}},\"properties\":{{" +
                        "\"address\":\"{2}\"," +
                        "\"latitude\":\"{1}\"," +
                        "\"longitude\":\"{0}\"," +
                        "\"name\":\"{3}\"," +
                        "\"oldName\":\"{6}\"," +
                        "\"capacity\":\"{4}\"," +
                        "\"operator\":\"{5}\"," +
                        "\"osmType\":\"{7}\"," +
                        "\"osmId\":\"{8}\"," +
                        "\"action\":\"reactivate\"}}}}]}}";

            return string.Format(
                template,
                GeoPoint.ParseCoords(current.lon).ToString(System.Globalization.CultureInfo.InvariantCulture),
                GeoPoint.ParseCoords(current.lat).ToString(System.Globalization.CultureInfo.InvariantCulture),
                current.id,
                current.name.Trim(),
                current.capacity,
                systemName,
                disused.name.Trim(),
                disused.osmType ?? "",
                disused.osmId ?? "");
        }
    }
}
