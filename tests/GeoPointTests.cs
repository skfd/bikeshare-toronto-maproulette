using NUnit.Framework;
using System.Globalization;

namespace prepareBikeParking.Tests;

public class GeoPointTests
{
    [Test]
    public void ParseLine_RoundTrip()
    {
        var gp = new GeoPoint{ id="10", name="Test Station", capacity=15, lat="43.123456", lon="-79.654321"};
        var line = GeoJsonGenerator.GenerateGeojsonLine(gp, "Sys");
        var parsed = GeoPoint.ParseLine(line);
        Assert.That(parsed.id, Is.EqualTo("10"));
        Assert.That(parsed.name, Is.EqualTo("Test Station"));
        Assert.That(parsed.capacity, Is.EqualTo(15));
        // Rounding to 5 decimals expected
        Assert.That(parsed.lat, Is.EqualTo("43.12346"));
    }

    [Test]
    public void NameWhitespaceCollapse()
    {
        var gp = new GeoPoint{ id="1", name="Name   With   Spaces", capacity=0, lat="1", lon="1"};
        Assert.That(gp.name, Is.EqualTo("Name With Spaces"));
    }

    [Test]
    public void CoordinateRounding_FiveDecimals()
    {
        var gp = new GeoPoint{ id="1", name="A", capacity=0, lat="43.1234567", lon="-79.0000099"};
        // Access properties should maintain original strings, rounding occurs in ParseLine/ParseCoords
        var line = GeoJsonGenerator.GenerateGeojsonLine(gp, "Sys");
        var parsed = GeoPoint.ParseLine(line);
        Assert.That(parsed.lat.Length - parsed.lat.IndexOf('.') - 1, Is.LessThanOrEqualTo(5));
    }

    [Test]
    public void MalformedJson_Throws()
    {
        var badLine = "\u001e{ not json";
        Assert.Throws<System.Text.Json.JsonException>(() => GeoPoint.ParseLine(badLine));
    }
}
