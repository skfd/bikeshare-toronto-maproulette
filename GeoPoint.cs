// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Text.Json.Nodes;

internal class GeoPoint
{
    public string id { get; set; }
    public string lat { get; set; }
    public string lon { get; set; }

    internal static GeoPoint ParseLine(string line)
    {

        var dynamicPoint = JsonSerializer.Deserialize<JsonObject>(line);

        var result = new GeoPoint
        {
            id = (string)dynamicPoint["features"][0]["properties"]["address"],
            lat = (string)dynamicPoint["features"][0]["properties"]["latitude"],
            lon = (string)dynamicPoint["features"][0]["properties"]["longitude"],
        };

        return result;
    }
}