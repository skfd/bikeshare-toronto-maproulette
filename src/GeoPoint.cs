// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public class GeoPoint
{
    public required string id { get; set; }

    private string _name = string.Empty;
    public required string name
    {
        get => _name;
    set => _name = string.IsNullOrEmpty(value) ? value ?? string.Empty : Regex.Replace(value, "\\s+", " ");
    }

    public int capacity { get; set; }
    public required string lat { get; set; }
    public required string lon { get; set; }
    public string? osmId { get; set; }
    public string? osmType { get; internal set; }
    public int osmVersion { get; internal set; }
    public JsonElement osmXmlElement { get; internal set; }

    public static GeoPoint ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) throw new ArgumentException("Line is null/empty", nameof(line));
        var trimmed = line.TrimStart('\u001e');
        var dynamicPoint = JsonSerializer.Deserialize<JsonObject>(trimmed) ?? throw new JsonException("Unable to deserialize GeoJSON line");

        try
        {
            var features = dynamicPoint["features"] as JsonArray ?? throw new JsonException("Missing features array");
            if (features.Count == 0) throw new JsonException("Features array empty");
            var feature = features[0] as JsonObject ?? throw new JsonException("First feature not an object");
            var props = feature["properties"] as JsonObject ?? throw new JsonException("Missing properties object");

            string GetProp(string key, bool required = true)
            {
                var v = props[key]?.GetValue<string>();
                if (v is null)
                {
                    if (required) throw new JsonException($"Missing required property '{key}'");
                    return string.Empty;
                }
                return v;
            }

            var id = GetProp("address");
            var name = GetProp("name");
            var capacityStr = GetProp("capacity", required: false);
            if (!int.TryParse(capacityStr, out var capacityVal)) capacityVal = 0;
            var latStr = GetProp("latitude");
            var lonStr = GetProp("longitude");

            return new GeoPoint
            {
                id = id,
                name = name.Trim(),
                capacity = capacityVal,
                lat = ParseCoords(latStr).ToString(System.Globalization.CultureInfo.InvariantCulture),
                lon = ParseCoords(lonStr).ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException("Failed to parse GeoPoint line", ex);
        }
    }

    public static double ParseCoords(string inp)
    {
        if (inp is null) throw new ArgumentNullException(nameof(inp));
        var result = Math.Round(double.Parse(inp, System.Globalization.CultureInfo.InvariantCulture), 5);
        return result;
    }
}
