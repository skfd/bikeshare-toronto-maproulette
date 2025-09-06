// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using prepareBikeParking;

internal class OsmFileFunctions
{
    internal static async Task GenerateRenameOsmChangeFile(List<(GeoPoint current, GeoPoint old)> renamedInOSM)
    {
        var osmChangeFilePath = "bikeshare_renames.osc";

        // Start with proper XML declaration with UTF-8 encoding and single root element
        var osmXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<osmChange version=\"0.6\" generator=\"prepareBikeParking\">\n";

        foreach (var (current, old) in renamedInOSM)
        {
            if (string.IsNullOrEmpty(old.osmId) || string.IsNullOrEmpty(old.osmType))
            {
                Console.WriteLine($"Skipping {old.id} as it lacks OSM ID or type.");
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
                    }
                    else
                    {
                        // Escape the existing value for XML
                        value = System.Security.SecurityElement.Escape(value);
                    }
                    
                    changeBlock += $"      <tag k=\"{System.Security.SecurityElement.Escape(key)}\" v=\"{value}\"/>\n";
                }
            }
            else
            {
                // Fallback: if no tags found, at least add the name tag
                changeBlock += $"      <tag k=\"name\" v=\"{newName}\"/>\n";
            }
            
            // Close the element
            changeBlock += $"    </{osmType}>\n  </modify>\n";
            
            osmXml += changeBlock;
        }

        // Close the modify and osmChange elements
        osmXml += "</osmChange>";

        await FileManager.WriteTextFileAsync(osmChangeFilePath, osmXml);
    }
}