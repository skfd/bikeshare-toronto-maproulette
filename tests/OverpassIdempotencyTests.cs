using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using prepareBikeParking;

namespace prepareBikeParking.Tests;

public class OverpassIdempotencyTests
{
    private const string SystemName = "IdempotencyTestSystem";
    private string OverpassPath => FileManager.GetSystemFullPath(SystemName, "stations.overpass");

    [SetUp]
    public void Clean()
    {
        var dir = FileManager.GetSystemFullPath(SystemName, "");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [Test]
    public async Task EnsureStationsOverpassFileAsync_DoesNotOverwriteExistingFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OverpassPath)!);
        var originalContent = "[out:json]; area[name=\"TestCity\"]->.city; node(area.city)[bicycle_rental=docking_station]; out meta;";
        await File.WriteAllTextAsync(OverpassPath, originalContent);

        // Call EnsureStationsOverpassFileAsync (should NOT overwrite)
        await OSMDataFetcher.EnsureStationsOverpassFileAsync(SystemName, "TestCity");
        var afterContent = await File.ReadAllTextAsync(OverpassPath);
        Assert.That(afterContent, Is.EqualTo(originalContent), "Existing stations.overpass should not be overwritten");
    }

    [TearDown]
    public void Cleanup()
    {
        var dir = FileManager.GetSystemFullPath(SystemName, "");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
}
