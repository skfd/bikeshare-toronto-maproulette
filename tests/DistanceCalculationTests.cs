using NUnit.Framework;

namespace prepareBikeParking.Tests;

public class DistanceCalculationTests
{
    [Test]
    public void ZeroDistance()
    {
        var d = BikeShareComparer.GetDistanceInMeters(43.0, -79.0, 43.0, -79.0);
        Assert.That(d, Is.EqualTo(0).Within(0.0001));
    }

    [Test]
    public void ApproxKnownDistance()
    {
        // Roughly ~111m per 0.001 latitude degree
        var d = BikeShareComparer.GetDistanceInMeters(0,0,0.001,0);
        Assert.That(d, Is.InRange(100, 120));
    }

    [Test]
    public void ThresholdBoundary_BelowNotMoved()
    {
        // Two points very close (<3m)
        var prev = new GeoPoint{ id="1", name="A", capacity=0, lat="43.000000", lon="-79.000000"};
        var curr = new GeoPoint{ id="1", name="A", capacity=0, lat="43.000010", lon="-79.000000"}; // ~1.1m
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(new(){ curr }, new(){ prev }, 3);
        Assert.That(moved, Is.Empty);
    }
}