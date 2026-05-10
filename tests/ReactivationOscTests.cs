using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace prepareBikeParking.Tests;

public class ReactivationOscTests
{
    private const string TempSystem = "TestSystem_Reactivation";

    [SetUp]
    public void CleanStart()
    {
        var full = prepareBikeParking.FileManager.GetSystemFullPath(TempSystem, "");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    private static GeoPoint DisusedFromJson(string osmJson)
    {
        var element = JsonDocument.Parse(osmJson).RootElement.Clone();
        var tags = element.GetProperty("tags");
        var refValue = tags.TryGetProperty("ref", out var refProp) ? refProp.GetString() ?? "" : "";
        var name = tags.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";

        return new GeoPoint
        {
            id = refValue,
            name = name,
            lat = element.GetProperty("lat").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            lon = element.GetProperty("lon").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            capacity = 0,
            IsDisused = true,
            osmId = element.GetProperty("id").GetInt64().ToString(),
            osmType = "node",
            osmVersion = element.GetProperty("version").GetInt32(),
            osmXmlElement = element,
        };
    }

    [Test]
    public async Task ReactivationOsc_DropsDisusedAmenity_AddsAmenityAndBicycleRental_WhenAbsent()
    {
        var disusedJson = """
        {
            "type": "node",
            "id": 100,
            "version": 3,
            "lat": 43.65,
            "lon": -79.4,
            "tags": {
                "disused:amenity": "bicycle_rental",
                "ref": "7000",
                "name": "Some Station",
                "operator": "Bike Share Toronto"
            }
        }
        """;

        var disused = DisusedFromJson(disusedJson);
        var current = new GeoPoint { id = "7000", name = "Some Station", capacity = 15, lat = "43.65", lon = "-79.4" };

        await OsmFileFunctions.GenerateReactivationOsmChangeFile(
            new List<(GeoPoint current, GeoPoint disused)> { (current, disused) }, TempSystem);

        var osc = await prepareBikeParking.FileManager.ReadSystemTextFileAsync(TempSystem, "bikeshare_reactivations.osc");

        Assert.That(osc, Does.Not.Contain("disused:amenity"));
        Assert.That(osc, Does.Contain("<tag k=\"amenity\" v=\"bicycle_rental\"/>"));
        Assert.That(osc, Does.Contain("<tag k=\"bicycle_rental\" v=\"docking_station\"/>"));
        Assert.That(osc, Does.Contain("<node id=\"100\" version=\"3\""));
        Assert.That(osc, Does.Contain("<tag k=\"operator\" v=\"Bike Share Toronto\"/>"));
    }

    [Test]
    public async Task ReactivationOsc_PreservesExistingBicycleRentalTag()
    {
        var disusedJson = """
        {
            "type": "node",
            "id": 101,
            "version": 5,
            "lat": 43.65,
            "lon": -79.4,
            "tags": {
                "disused:amenity": "bicycle_rental",
                "bicycle_rental": "docking_station",
                "ref": "7001",
                "name": "Existing Tag Station"
            }
        }
        """;

        var disused = DisusedFromJson(disusedJson);
        var current = new GeoPoint { id = "7001", name = "Existing Tag Station", capacity = 10, lat = "43.65", lon = "-79.4" };

        await OsmFileFunctions.GenerateReactivationOsmChangeFile(
            new List<(GeoPoint current, GeoPoint disused)> { (current, disused) }, TempSystem);

        var osc = await prepareBikeParking.FileManager.ReadSystemTextFileAsync(TempSystem, "bikeshare_reactivations.osc");

        var bicycleRentalCount = System.Text.RegularExpressions.Regex.Matches(osc, "<tag k=\"bicycle_rental\"").Count;
        Assert.That(bicycleRentalCount, Is.EqualTo(1), "bicycle_rental tag should appear exactly once");
        Assert.That(osc, Does.Contain("<tag k=\"bicycle_rental\" v=\"docking_station\"/>"));
    }

    [Test]
    public async Task ReactivationOsc_UpdatesNameWhenChanged()
    {
        var disusedJson = """
        {
            "type": "node",
            "id": 102,
            "version": 7,
            "lat": 43.65,
            "lon": -79.4,
            "tags": {
                "disused:amenity": "bicycle_rental",
                "ref": "7002",
                "name": "Old Name"
            }
        }
        """;

        var disused = DisusedFromJson(disusedJson);
        var current = new GeoPoint { id = "7002", name = "New Name", capacity = 12, lat = "43.65", lon = "-79.4" };

        await OsmFileFunctions.GenerateReactivationOsmChangeFile(
            new List<(GeoPoint current, GeoPoint disused)> { (current, disused) }, TempSystem);

        var osc = await prepareBikeParking.FileManager.ReadSystemTextFileAsync(TempSystem, "bikeshare_reactivations.osc");

        Assert.That(osc, Does.Contain("<tag k=\"name\" v=\"New Name\"/>"));
        Assert.That(osc, Does.Not.Contain("Old Name"));
    }

    [Test]
    public async Task ReactivationOsc_AddsNameWhenOsmHasNone()
    {
        var disusedJson = """
        {
            "type": "node",
            "id": 103,
            "version": 1,
            "lat": 43.65,
            "lon": -79.4,
            "tags": {
                "disused:amenity": "bicycle_rental",
                "ref": "7003"
            }
        }
        """;

        var disused = DisusedFromJson(disusedJson);
        var current = new GeoPoint { id = "7003", name = "Fresh Name", capacity = 8, lat = "43.65", lon = "-79.4" };

        await OsmFileFunctions.GenerateReactivationOsmChangeFile(
            new List<(GeoPoint current, GeoPoint disused)> { (current, disused) }, TempSystem);

        var osc = await prepareBikeParking.FileManager.ReadSystemTextFileAsync(TempSystem, "bikeshare_reactivations.osc");

        Assert.That(osc, Does.Contain("<tag k=\"name\" v=\"Fresh Name\"/>"));
    }
}
