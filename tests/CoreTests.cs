using NUnit.Framework;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace prepareBikeParking.Tests;

public class CoreTests
{
    [Test]
    public void BikeShareComparer_DetectsAddedRemovedMovedRenamed()
    {
        var previous = new List<GeoPoint>{ new GeoPoint{ id="1", name="Old Name", capacity=10, lat="43.0", lon="-79.0" } };
        var current = new List<GeoPoint>{
            new GeoPoint{ id="1", name="New Name", capacity=10, lat="43.0", lon="-79.0"}, // rename
            new GeoPoint{ id="2", name="Second", capacity=5, lat="43.0001", lon="-79.0001"} // added
        };

        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(current, previous, moveThreshold: 3);
        Assert.That(added.Select(x=>x.id), Is.EquivalentTo(new[]{"2"}));
        Assert.That(removed, Is.Empty);
        Assert.That(moved, Is.Empty);
        Assert.That(renamed.Count, Is.EqualTo(1));
        Assert.That(renamed[0].old.name, Is.EqualTo("Old Name"));
    }

    [Test]
    public void GeoJsonGenerator_GeneratesExpectedLine()
    {
        var p = new GeoPoint{ id="123", name="Station A", capacity=12, lat="43.1", lon="-79.2" };
        var line = prepareBikeParking.GeoJsonGenerator.GenerateGeojsonLine(p, "SystemX");
        Assert.That(line.StartsWith("\u001e{"));
        Assert.That(line.Contains("\"name\":\"Station A\""));
        Assert.That(line.Contains("SystemX"));
    }

    [Test]
    public void GeoPoint_ParseLine_RoundTrip()
    {
        var gp = new GeoPoint{ id="555", name="Test Station", capacity=7, lat="43.20000", lon="-79.30000" };
        var line = prepareBikeParking.GeoJsonGenerator.GenerateGeojsonLine(gp, "Sys");
        var parsed = GeoPoint.ParseLine(line);
        Assert.That(parsed.id, Is.EqualTo("555"));
        Assert.That(parsed.name, Is.EqualTo("Test Station"));
        Assert.That(parsed.capacity, Is.EqualTo(7));
        Assert.That(parsed.lat, Is.EqualTo("43.2"));
    }
}
