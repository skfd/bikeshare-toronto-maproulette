using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

    [Test]
    public void Escaping_SpecialCharactersPreserved()
    {
        var p = Pt("3","Station, \"Quoted\" & More",43.2,-79.4);
        var line = GeoJsonGenerator.GenerateGeojsonLine(p, "Sys");
        Assert.That(line.Contains("Station, "));
        Assert.That(line.Contains("\"Quoted\""), "Quotes should appear escaped in JSON output");
        Assert.That(line.Contains("& More"));
    }

    [Test]
    public async Task OSMComparison_WritesClosedFile()
    {
        const string sys = "TestSystem_Closed_GJ";
        var dir = FileManager.GetSystemFullPath(sys, "");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);

        var closed = new List<GeoPoint> { Pt("9", "Closed Station", 43.5, -79.5, 10) };
        await GeoJsonGenerator.GenerateOSMComparisonFilesAsync(
            new List<GeoPoint>(), new List<GeoPoint>(), new List<GeoPoint>(),
            new List<(GeoPoint current, GeoPoint old)>(), closed, sys);

        var path = FileManager.GetSystemFullPath(sys, "bikeshare_closed.geojson");
        Assert.That(File.Exists(path), Is.True, "bikeshare_closed.geojson should be written");
        var text = await File.ReadAllTextAsync(path);
        Assert.That(text.Contains("\"name\":\"Closed Station\""));
        Assert.That(text.Trim().Split('\n').Length, Is.EqualTo(1));

        Directory.Delete(dir, true);
    }
}
