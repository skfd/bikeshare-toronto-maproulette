using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace prepareBikeParking.Tests;

public class ComparerTests
{
    private GeoPoint Pt(string id, string name, double lat, double lon, int capacity=0)
        => new GeoPoint{ id=id, name=name, capacity=capacity, lat=lat.ToString(System.Globalization.CultureInfo.InvariantCulture), lon=lon.ToString(System.Globalization.CultureInfo.InvariantCulture)};

    [Test]
    public void AddedAndRemovedDetected()
    {
        var prev = new List<GeoPoint>{ Pt("1","A",43, -79), Pt("2","B",43.1,-79.1)};
        var curr = new List<GeoPoint>{ Pt("2","B",43.1,-79.1), Pt("3","C",43.2,-79.2)};
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(curr, prev, 3);
        Assert.That(added.Select(p=>p.id), Is.EquivalentTo(new[]{"3"}));
        Assert.That(removed.Select(p=>p.id), Is.EquivalentTo(new[]{"1"}));
        Assert.That(moved, Is.Empty);
        Assert.That(renamed, Is.Empty);
    }

    [Test]
    public void MoveThresholdBoundary()
    {
        // Points differ slightly; choose coordinates so distance ~4m (>3m threshold)
        var prev = new List<GeoPoint>{ Pt("1","A",43.000000,-79.000000)};
        var curr = new List<GeoPoint>{ Pt("1","A",43.000030,-79.000000)}; // ~3.3m North
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(curr, prev, 3);
        Assert.That(moved.Select(p=>p.id), Is.EquivalentTo(new[]{"1"}));
        Assert.That(added, Is.Empty);
        Assert.That(removed, Is.Empty);
        Assert.That(renamed, Is.Empty);
    }

    [Test]
    public void RenameOnly_NotMoved()
    {
        var prev = new List<GeoPoint>{ Pt("1","Old Name",43,-79)};
        var curr = new List<GeoPoint>{ Pt("1","New Name",43,-79)};
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(curr, prev, 3);
        Assert.That(renamed.Count, Is.EqualTo(1));
        Assert.That(renamed[0].old.name, Is.EqualTo("Old Name"));
        Assert.That(moved, Is.Empty);
    }

    [Test]
    public void RenameWithWhitespaceNormalization()
    {
        var prev = new List<GeoPoint>{ Pt("1","Station   Name",43,-79)}; // multiple spaces
        var curr = new List<GeoPoint>{ Pt("1","Station Name",43,-79)}; // single space
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(curr, prev, 3);
        // Because GeoPoint normalizes on set, these should be considered identical (no rename)
        Assert.That(renamed, Is.Empty);
        Assert.That(moved, Is.Empty);
    }

    [Test]
    public void NoOpIdenticalLists()
    {
        var prev = new List<GeoPoint>{ Pt("1","Same",43,-79)};
        var curr = new List<GeoPoint>{ Pt("1","Same",43,-79)};
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(curr, prev, 3);
        Assert.That(added, Is.Empty);
        Assert.That(removed, Is.Empty);
        Assert.That(moved, Is.Empty);
        Assert.That(renamed, Is.Empty);
    }

    [Test]
    public void RenamedAndMoved_ClassifiedAsMovedOnly()
    {
        var prev = new List<GeoPoint>{ Pt("1","Old Name",43.000000,-79.000000)};
        // Move north slightly over threshold AND rename
        var curr = new List<GeoPoint>{ Pt("1","New Name",43.000040,-79.000000)}; // ~4.4m
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(curr, prev, 3);
        Assert.That(moved.Select(p=>p.id), Is.EquivalentTo(new[]{"1"}));
        Assert.That(renamed, Is.Empty, "Should not list renamed when also moved");
    }
}
