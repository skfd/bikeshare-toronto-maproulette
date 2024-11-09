// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Text.Json.Nodes;

internal class GeoPoint
{
    public string id { get; set; }
    public string name { get; set; }
    public int capacity { get; set; }
    public string lat { get; set; }
    public string lon { get; set; }

    internal static GeoPoint ParseLine(string line)
    {

        var dynamicPoint = JsonSerializer.Deserialize<JsonObject>(line.TrimStart('\u001e'));

        var result = new GeoPoint
        {
            id = (string)dynamicPoint["features"][0]["properties"]["address"],
            name = ((string)dynamicPoint["features"][0]["properties"]["name"]).Trim(),
            capacity = int.Parse((string)dynamicPoint["features"][0]["properties"]["capacity"] ?? "0"),
            lat = ParseCoords((string)dynamicPoint["features"][0]["properties"]["latitude"]).ToString(System.Globalization.CultureInfo.InvariantCulture),
            lon = ParseCoords((string)dynamicPoint["features"][0]["properties"]["longitude"]).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        return result;
    }

    public static double ParseCoords(string inp)
    {
        var result = Math.Round(double.Parse(inp, System.Globalization.CultureInfo.InvariantCulture), 5);
        return result;
    }
}
