using NUnit.Framework;
using System.Globalization;
using System.Threading;

namespace prepareBikeParking.Tests;

public class CultureFormattingTests
{
    [Test]
    public void DecimalPointInvariant_DeDECulture()
    {
        var prev = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");
        try
        {
            var p = new GeoPoint{ id="1", name="A", capacity=0, lat="43.123456", lon="-79.654321" };
            var line = GeoJsonGenerator.GenerateGeojsonLine(p, "Sys");
            Assert.That(line.Contains("43.12346"));
            Assert.That(!line.Contains("43,12346"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prev;
            Thread.CurrentThread.CurrentUICulture = prev;
        }
    }

    [Test]
    public void LatLonRoundtripPreserved()
    {
        var p = new GeoPoint{ id="2", name="B", capacity=0, lat="43.98765", lon="-79.12345" };
        var line = GeoJsonGenerator.GenerateGeojsonLine(p, "Sys");
        var parsed = GeoPoint.ParseLine(line);
        Assert.That(parsed.lat.StartsWith("43.987"));
        Assert.That(parsed.lon.StartsWith("-79.123"));
    }
}
