using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace prepareBikeParking.Tests;

public class GeoJsonGeneratorTests
{
    private GeoPoint Pt(string id, string name, double lat, double lon, int capacity=0)
        => new GeoPoint{ id=id, name=name, capacity=capacity, lat=lat.ToString(System.Globalization.CultureInfo.InvariantCulture), lon=lon.ToString(System.Globalization.CultureInfo.InvariantCulture)};

    [Test]
    public void GenerateLine_HasRecordSeparatorAndProperties()
    {
        var p = Pt("100","Station",43.1,-79.2,12);
        var line = GeoJsonGenerator.GenerateGeojsonLine(p, "Sys");
        Assert.That(line[0], Is.EqualTo('\u001e'));
        Assert.That(line.Contains("\"name\":\"Station\""));
        Assert.That(line.Contains("\"capacity\":\"12\""));
    }

    [Test]
    public void RenamedLine_IncludesOldName()
    {
        var current = Pt("1","NewName",43,-79);
        var line = GeoJsonGenerator.GenerateGeojsonLineWithOldName(current, "OldName", "Sys");
        Assert.That(line.Contains("\"oldName\":\"OldName\""));
        Assert.That(line.Contains("\"name\":\"NewName\""));
    }

    [Test]
    public void InvariantDecimalFormat()
    {
        var p = Pt("2","Name",43.123456,-79.654321);
        var line = GeoJsonGenerator.GenerateGeojsonLine(p, "Sys");
        // Ensure decimal separator is dot
        Assert.That(line.Contains("43.12346"));
        Assert.That(!line.Contains(",123"));
    }
}
