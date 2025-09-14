using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace prepareBikeParking.Tests;

public class PerformanceSecurityTests
{
    [Test]
    public void CompareLargeStationList_PerformanceSmoke()
    {
        var prev = new List<GeoPoint>();
        var curr = new List<GeoPoint>();
        for (int i = 0; i < 5000; i++)
        {
            prev.Add(new GeoPoint{ id=$"{i}", name=$"Station {i}", capacity=10, lat="43.0", lon="-79.0" });
            curr.Add(new GeoPoint{ id=$"{i}", name=$"Station {i}", capacity=10, lat="43.0", lon="-79.0" });
        }
        var sw = Stopwatch.StartNew();
        var (added, removed, moved, renamed) = BikeShareComparer.ComparePoints(curr, prev, 3);
        sw.Stop();
        Assert.That(added, Is.Empty);
        Assert.That(removed, Is.Empty);
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), "Should run under 2s");
    }

    [Test]
    public void InvalidGbfsUrl_Throws()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await BikeShareDataFetcher.FetchFromApiAsync(""));
    }

    [Test]
    public void PathSanitization_SystemNameDoesNotEscapeRoot()
    {
        var sys = "../evil";
        var path = FileManager.GetSystemFullPath(sys, "file.txt");
        Assert.That(path.Replace('\\','/').Contains("data_results/evil/file.txt"));
        Assert.That(!path.Contains(".."));
    }
}
