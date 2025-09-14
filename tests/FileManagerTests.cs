using NUnit.Framework;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace prepareBikeParking.Tests;

public class FileManagerTests
{
    private string _tempSystem = "TestSystem_FM";

    [SetUp]
    public void CleanStart()
    {
        var full = prepareBikeParking.FileManager.GetSystemFullPath(_tempSystem, "");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    [Test]
    public async Task WriteAndReadText_Works()
    {
        await prepareBikeParking.FileManager.WriteSystemTextFileAsync(_tempSystem, "sample.txt", "hello world");
        var read = await prepareBikeParking.FileManager.ReadSystemTextFileAsync(_tempSystem, "sample.txt");
        Assert.That(read, Is.EqualTo("hello world"));
    }

    private record Demo(string A, int B);

    [Test]
    public async Task WriteAndReadJson_Works()
    {
        var obj = new Demo("value", 3);
        await prepareBikeParking.FileManager.WriteJsonFileAsync("data_results/" + _tempSystem + "/demo.json", obj);
        var round = await prepareBikeParking.FileManager.ReadJsonFileAsync<Demo>("data_results/" + _tempSystem + "/demo.json");
        Assert.That(round.A, Is.EqualTo("value"));
        Assert.That(round.B, Is.EqualTo(3));
    }

    [Test]
    public async Task WriteGeoJsonFile_OrdersById()
    {
        var pts = new List<GeoPoint>{
            new GeoPoint{ id="b", name="B", capacity=0, lat="1", lon="1"},
            new GeoPoint{ id="a", name="A", capacity=0, lat="1", lon="1"}
        };
        await prepareBikeParking.FileManager.WriteGeoJsonFileAsync("data_results/"+_tempSystem+"/ordered.geojson", pts, p => prepareBikeParking.GeoJsonGenerator.GenerateGeojsonLine(p, "Sys"));
        var text = await prepareBikeParking.FileManager.ReadTextFileAsync("data_results/"+_tempSystem+"/ordered.geojson");
        var lines = text.Split('\n');
        Assert.That(lines.Length, Is.EqualTo(2));
        var firstParsed = GeoPoint.ParseLine(lines[0]);
        Assert.That(firstParsed.id, Is.EqualTo("a"));
    }

    [Test]
    public void ReadMissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => prepareBikeParking.FileManager.ReadTextFile("data_results/"+_tempSystem+"/nope.txt"));
    }

    [Test]
    public async Task WriteGeoJsonWithOldNames_OrdersById()
    {
        var pairs = new List<(GeoPoint current, GeoPoint old)>{
            (new GeoPoint{ id="b", name="B New", capacity=0, lat="1", lon="1"}, new GeoPoint{ id="b", name="B Old", capacity=0, lat="1", lon="1"}),
            (new GeoPoint{ id="a", name="A New", capacity=0, lat="1", lon="1"}, new GeoPoint{ id="a", name="A Old", capacity=0, lat="1", lon="1"})
        };
        await prepareBikeParking.FileManager.WriteGeoJsonFileWithOldNamesAsync("data_results/"+_tempSystem+"/renamed.geojson", pairs, (gp, oldName) => prepareBikeParking.GeoJsonGenerator.GenerateGeojsonLineWithOldName(gp, oldName, "Sys"));
        var text = await prepareBikeParking.FileManager.ReadTextFileAsync("data_results/"+_tempSystem+"/renamed.geojson");
        var lines = text.Split('\n');
        Assert.That(lines.Length, Is.EqualTo(2));
        var firstParsed = GeoPoint.ParseLine(lines[0]);
        Assert.That(firstParsed.id, Is.EqualTo("a"));
        Assert.That(lines[0].Contains("\"oldName\":\"A Old\""));
    }

    [Test]
    public void GetFullPath_EndsWithRelative()
    {
        var full = prepareBikeParking.FileManager.GetFullPath("data_results/"+_tempSystem+"/file.txt");
        Assert.That(full.Replace('\\','/').EndsWith("data_results/"+_tempSystem+"/file.txt"));
    }

    private class NullRootDemo { }

    [Test]
    public async Task ReadJsonFile_NullRoot_Throws()
    {
        var rel = "data_results/"+_tempSystem+"/bad.json";
        // Write literal 'null' JSON
        await prepareBikeParking.FileManager.WriteTextFileAsync(rel, "null");
        Assert.ThrowsAsync<InvalidOperationException>(async () => await prepareBikeParking.FileManager.ReadJsonFileAsync<NullRootDemo>(rel));
    }

    [TearDown]
    public void Cleanup()
    {
        var full = prepareBikeParking.FileManager.GetSystemFullPath(_tempSystem, "");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }
}
