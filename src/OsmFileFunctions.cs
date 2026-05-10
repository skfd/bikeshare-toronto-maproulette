// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using Serilog;
using prepareBikeParking;

internal class OsmFileFunctions
{
    internal static async Task GenerateRenameOsmChangeFile(List<(GeoPoint current, GeoPoint old)> renamedInOSM, string systemName)
    {
        var osmChangeFileName = "bikeshare_renames.osc";

        // Start with proper XML declaration with UTF-8 encoding and single root element
        var osmXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<osmChange version=\"0.6\" generator=\"prepareBikeParking\">\n";

        foreach (var (current, old) in renamedInOSM)
        {
            if (string.IsNullOrEmpty(old.osmId) || string.IsNullOrEmpty(old.osmType))
            {
                Log.Debug("Skipping rename for station {Id} due to missing OSM id/type", old.id);
                continue;
            }

            var osmId = old.osmId;
            var osmType = old.osmType.ToLower();
            var osmVersion = old.osmVersion;
            var newName = System.Security.SecurityElement.Escape(current.name);

            // Start the element with id and bumped version
            var changeBlock = $"  <modify>\n    <{osmType} id=\"{osmId}\" version=\"{osmVersion}\"";

            // For node elements, add lat/lon coordinates (required for OSM changesets)
            if (osmType == "node")
            {
                if (old.osmXmlElement.TryGetProperty("lat", out var latProperty))
                {
                    var lat = latProperty.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    changeBlock += $" lat=\"{lat}\"";
                }

                if (old.osmXmlElement.TryGetProperty("lon", out var lonProperty))
                {
                    var lon = lonProperty.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    changeBlock += $" lon=\"{lon}\"";
                }
            }

            changeBlock += ">\n";

            // For way elements, add the nodes first
            if (osmType == "way" && old.osmXmlElement.TryGetProperty("nodes", out var nodesProperty) && nodesProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var nodeId in nodesProperty.EnumerateArray())
                {
                    changeBlock += $"      <nd ref=\"{nodeId.GetInt64()}\"/>\n";
                }
            }

            // Copy all existing tags from the original OSM element, updating the name
            var nameEmitted = false;
            if (old.osmXmlElement.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
            {
                foreach (var tag in tags.EnumerateObject())
                {
                    var key = tag.Name;
                    var value = tag.Value.GetString() ?? "";

                    // Use the new name for the name tag, keep all other tags as they were
                    if (key == "name")
                    {
                        value = newName;
                        nameEmitted = true;
                    }
                    else
                    {
                        // Escape the existing value for XML
                        value = System.Security.SecurityElement.Escape(value);
                    }

                    changeBlock += $"      <tag k=\"{System.Security.SecurityElement.Escape(key)}\" v=\"{value}\"/>\n";
                }
            }

            // Original OSM element had no name tag — add one so the rename actually applies.
            if (!nameEmitted)
            {
                changeBlock += $"      <tag k=\"name\" v=\"{newName}\"/>\n";
            }

            // Close the element
            changeBlock += $"    </{osmType}>\n  </modify>\n";

            osmXml += changeBlock;
        }

        // Close the modify and osmChange elements
        osmXml += "</osmChange>";

        await FileManager.WriteSystemTextFileAsync(systemName, osmChangeFileName, osmXml);
    }

    internal static async Task GenerateReactivationOsmChangeFile(List<(GeoPoint current, GeoPoint disused)> reactivations, string systemName)
    {
        var osmChangeFileName = "bikeshare_reactivations.osc";

        var osmXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<osmChange version=\"0.6\" generator=\"prepareBikeParking\">\n";

        foreach (var (current, disused) in reactivations)
        {
            if (string.IsNullOrEmpty(disused.osmId) || string.IsNullOrEmpty(disused.osmType))
            {
                Log.Debug("Skipping reactivation for station {Id} due to missing OSM id/type", current.id);
                continue;
            }

            var osmId = disused.osmId;
            var osmType = disused.osmType.ToLower();
            var osmVersion = disused.osmVersion;
            var newName = System.Security.SecurityElement.Escape(current.name.Trim());

            var changeBlock = $"  <modify>\n    <{osmType} id=\"{osmId}\" version=\"{osmVersion}\"";

            if (osmType == "node")
            {
                if (disused.osmXmlElement.TryGetProperty("lat", out var latProperty))
                {
                    var lat = latProperty.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    changeBlock += $" lat=\"{lat}\"";
                }

                if (disused.osmXmlElement.TryGetProperty("lon", out var lonProperty))
                {
                    var lon = lonProperty.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    changeBlock += $" lon=\"{lon}\"";
                }
            }

            changeBlock += ">\n";

            if (osmType == "way" && disused.osmXmlElement.TryGetProperty("nodes", out var nodesProperty) && nodesProperty.ValueKind == JsonValueKind.Array)
            {
                foreach (var nodeId in nodesProperty.EnumerateArray())
                {
                    changeBlock += $"      <nd ref=\"{nodeId.GetInt64()}\"/>\n";
                }
            }

            var amenityEmitted = false;
            var bicycleRentalEmitted = false;
            var nameEmitted = false;
            if (disused.osmXmlElement.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
            {
                foreach (var tag in tags.EnumerateObject())
                {
                    var key = tag.Name;
                    var value = tag.Value.GetString() ?? "";

                    if (key == "disused:amenity")
                    {
                        continue;
                    }

                    if (key == "amenity")
                    {
                        if (value != "bicycle_rental")
                        {
                            Log.Warning("Reactivating station {Id} (OSM {OsmType}/{OsmId}) overrode existing amenity={ExistingAmenity} -> bicycle_rental",
                                current.id, osmType, osmId, value);
                        }
                        value = "bicycle_rental";
                        amenityEmitted = true;
                    }
                    else if (key == "bicycle_rental")
                    {
                        value = System.Security.SecurityElement.Escape(value);
                        bicycleRentalEmitted = true;
                    }
                    else if (key == "name")
                    {
                        value = newName;
                        nameEmitted = true;
                    }
                    else
                    {
                        value = System.Security.SecurityElement.Escape(value);
                    }

                    changeBlock += $"      <tag k=\"{System.Security.SecurityElement.Escape(key)}\" v=\"{value}\"/>\n";
                }
            }

            if (!amenityEmitted)
            {
                changeBlock += $"      <tag k=\"amenity\" v=\"bicycle_rental\"/>\n";
            }

            if (!bicycleRentalEmitted)
            {
                changeBlock += $"      <tag k=\"bicycle_rental\" v=\"docking_station\"/>\n";
            }

            if (!nameEmitted && !string.IsNullOrEmpty(current.name))
            {
                changeBlock += $"      <tag k=\"name\" v=\"{newName}\"/>\n";
            }

            changeBlock += $"    </{osmType}>\n  </modify>\n";

            osmXml += changeBlock;
        }

        osmXml += "</osmChange>";

        await FileManager.WriteSystemTextFileAsync(systemName, osmChangeFileName, osmXml);
    }
}