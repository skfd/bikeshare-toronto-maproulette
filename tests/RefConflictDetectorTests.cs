using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using prepareBikeParking;

namespace prepareBikeParking.Tests;

public class RefConflictDetectorTests
{
    private const string TempSystem = "TestSystem_RefConflicts";
    private const string OscFile = "bikeshare_ref_conflicts.osc";
    private const string GeoFile = "bikeshare_ref_conflicts.geojson";
    private const string SystemId = "test_sys";

    // Default thresholds used in production: 30 m OSM comparison, 300 m ref-conflict.
    private const double OsmThreshold = 30.0;
    private const double RefConflictThreshold = 300.0;

    [SetUp]
    public void CleanStart()
    {
        var full = FileManager.GetSystemFullPath(TempSystem, "");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    private static GeoPoint Gbfs(string id, string name, double lat, double lon) =>
        new GeoPoint { id = id, name = name, capacity = 0, lat = lat.ToString(System.Globalization.CultureInfo.InvariantCulture), lon = lon.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    private static GeoPoint OsmNode(long osmId, int version, string name, double lat, double lon, string? refValue)
    {
        var tags = new System.Text.StringBuilder();
        tags.Append("{");
        tags.Append($"\"name\":\"{name}\"");
        if (refValue != null) tags.Append($",\"ref\":\"{refValue}\"");
        tags.Append(",\"amenity\":\"bicycle_rental\",\"bicycle_rental\":\"docking_station\"");
        tags.Append("}");
        var latS = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lonS = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var json = $"{{\"type\":\"node\",\"id\":{osmId},\"version\":{version},\"lat\":{latS},\"lon\":{lonS},\"tags\":{tags}}}";
        var element = JsonDocument.Parse(json).RootElement.Clone();
        return new GeoPoint
        {
            id = refValue ?? $"osm_{osmId}",
            name = name,
            capacity = 0,
            lat = latS,
            lon = lonS,
            osmId = osmId.ToString(),
            osmType = "node",
            osmVersion = version,
            osmXmlElement = element,
        };
    }

    [Test]
    public void Orphan_NoRef_NameMatchesGbfsNearby_ProducesFixRef()
    {
        var gbfs = new List<GeoPoint> { Gbfs("73", "Foo / Bar", 43.65010, -79.40000) };
        var osm = new List<GeoPoint> { OsmNode(111, 1, "Foo / Bar", 43.65000, -79.40000, refValue: null) };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts.Count, Is.EqualTo(1));
        var c = result.Conflicts[0];
        Assert.That(c.Kind, Is.EqualTo("fix-ref"));
        Assert.That(c.ResolvedGbfs!.id, Is.EqualTo("73"));
        Assert.That(c.CurrentRef, Is.Null);
        Assert.That(result.SuppressedGbfsIds, Does.Contain("73"));
    }

    [Test]
    public void Orphan_NoRef_NameMatchesButFarAway_NoConflict()
    {
        var gbfs = new List<GeoPoint> { Gbfs("74", "Baz / Qux", 44.00, -80.00) };
        var osm = new List<GeoPoint> { OsmNode(112, 1, "Baz / Qux", 43.65, -79.40, refValue: null) };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts, Is.Empty);
        Assert.That(result.SuppressedGbfsIds, Is.Empty);
    }

    [Test]
    public void Orphan_NoRef_NoGbfsNameMatch_NoConflict()
    {
        var gbfs = new List<GeoPoint> { Gbfs("75", "Something Else", 43.65, -79.40) };
        var osm = new List<GeoPoint> { OsmNode(113, 1, "Unmatched Name", 43.65, -79.40, refValue: null) };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts, Is.Empty);
    }

    [Test]
    public void Orphan_NameMatchIsCaseAndWhitespaceInsensitive()
    {
        var gbfs = new List<GeoPoint> { Gbfs("90", "McGill College / de Maisonneuve", 43.65010, -79.40000) };
        var osm = new List<GeoPoint> { OsmNode(114, 1, "  mcgill college  /  DE maisonneuve ", 43.65000, -79.40000, refValue: null) };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts.Count, Is.EqualTo(1));
        Assert.That(result.Conflicts[0].Kind, Is.EqualTo("fix-ref"));
        Assert.That(result.Conflicts[0].ResolvedGbfs!.id, Is.EqualTo("90"));
    }

    [Test]
    public void OsmNode_WithRef_FarFromSameIdGbfs_ProducesReviewRef_AndSuppressesId()
    {
        // GBFS 500 is now a totally different, far-away station; OSM still has ref=500 on the old node.
        var gbfs = new List<GeoPoint> { Gbfs("500", "New Far Station", 44.00, -80.00) };
        var osm = new List<GeoPoint> { OsmNode(200, 3, "Old Station Name", 43.65, -79.40, refValue: "500") };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts.Count, Is.EqualTo(1));
        var c = result.Conflicts[0];
        Assert.That(c.Kind, Is.EqualTo("review-ref"));
        Assert.That(c.CurrentRef, Is.EqualTo("500"));
        Assert.That(c.ClaimedGbfsName, Is.EqualTo("New Far Station"));
        Assert.That(c.ResolvedGbfs, Is.Null);
        Assert.That(result.SuppressedGbfsIds, Does.Contain("500"));
    }

    [Test]
    public void OsmNode_WithRef_NearSameIdGbfs_NoConflict()
    {
        // ~22 m apart — a plausible move, not a recycled id.
        var gbfs = new List<GeoPoint> { Gbfs("501", "Same Station", 43.65020, -79.40000) };
        var osm = new List<GeoPoint> { OsmNode(201, 1, "Same Station", 43.65000, -79.40000, refValue: "501") };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts, Is.Empty);
    }

    [Test]
    public void Ambiguous_TwoGbfsSameNameNearby_NoConflict_Warning()
    {
        var gbfs = new List<GeoPoint>
        {
            Gbfs("10", "Dup Name", 43.65002, -79.40000),
            Gbfs("11", "Dup Name", 43.65006, -79.40000),
        };
        var osm = new List<GeoPoint> { OsmNode(300, 1, "Dup Name", 43.65000, -79.40000, refValue: null) };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts, Is.Empty);
        Assert.That(result.Warnings, Is.Not.Empty);
    }

    [Test]
    public void RecycleChain_FixesOrphan_FlagsStaleRefNode()
    {
        // Realistic shape: GBFS 73 = "McGill College / de Maisonneuve" (currently mapped only as a ref-less OSM node);
        // OSM still has ref=73 on the old "Mackay / de Maisonneuve" node ~700 m away.
        var gbfs = new List<GeoPoint>
        {
            Gbfs("73", "McGill College / de Maisonneuve", 45.50276, -73.57255),
            Gbfs("89", "Mackay / de Maisonneuve", 45.49681, -73.57911),
        };
        var osm = new List<GeoPoint>
        {
            OsmNode(13795673649, 1, "McGill College / de Maisonneuve", 45.50265, -73.57254, refValue: null),
            OsmNode(777, 5, "Mackay / de Maisonneuve", 45.49684, -73.57923, refValue: "73"),
        };

        var result = RefConflictDetector.Detect(gbfs, osm, OsmThreshold, RefConflictThreshold);

        Assert.That(result.Conflicts.Count, Is.EqualTo(2));
        var fix = result.Conflicts.Single(c => c.Kind == "fix-ref");
        Assert.That(fix.OsmNode.osmId, Is.EqualTo("13795673649"));
        Assert.That(fix.ResolvedGbfs!.id, Is.EqualTo("73"));

        var review = result.Conflicts.Single(c => c.Kind == "review-ref");
        Assert.That(review.OsmNode.osmId, Is.EqualTo("777"));
        Assert.That(review.CurrentRef, Is.EqualTo("73"));
        // Best-effort resolution: the old node is near GBFS 89 and shares its name.
        Assert.That(review.ResolvedGbfs!.id, Is.EqualTo("89"));

        Assert.That(result.SuppressedGbfsIds, Is.EquivalentTo(new[] { "73" }));
    }

    [Test]
    public async Task GenerateRefConflictOsmChangeFile_WritesRefAndRefGbfs_ForFixRef()
    {
        var conflicts = new List<RefConflictDetector.RefConflict>
        {
            new RefConflictDetector.RefConflict(
                OsmNode(13795673649, 1, "McGill College / de Maisonneuve", 45.50265, -73.57254, refValue: null),
                CurrentRef: null,
                ResolvedGbfs: Gbfs("73", "McGill College / de Maisonneuve", 45.50276, -73.57255),
                ResolvedDistanceMeters: 12,
                ClaimedGbfsName: null,
                ClaimedDistanceMeters: null,
                Kind: "fix-ref"),
        };

        await OsmFileFunctions.GenerateRefConflictOsmChangeFile(conflicts, TempSystem, SystemId);

        var osc = await FileManager.ReadSystemTextFileAsync(TempSystem, OscFile);
        Assert.That(osc, Does.Contain("<node id=\"13795673649\" version=\"1\""));
        Assert.That(osc, Does.Contain("<tag k=\"ref\" v=\"73\"/>"));
        Assert.That(osc, Does.Contain("<tag k=\"ref:gbfs\" v=\"test_sys:73\"/>"));
        Assert.That(osc, Does.Contain("<tag k=\"amenity\" v=\"bicycle_rental\"/>"));
    }

    [Test]
    public async Task GenerateRefConflictOsmChangeFile_NoAutoFixes_WritesNoFile_AndRemovesStale()
    {
        // Pre-create a stale file.
        await FileManager.WriteSystemTextFileAsync(TempSystem, OscFile, "stale");
        Assert.That(File.Exists(FileManager.GetSystemFullPath(TempSystem, OscFile)), Is.True);

        var conflicts = new List<RefConflictDetector.RefConflict>
        {
            new RefConflictDetector.RefConflict(
                OsmNode(200, 3, "Old Station", 43.65, -79.40, refValue: "500"),
                CurrentRef: "500", ResolvedGbfs: null, ResolvedDistanceMeters: null,
                ClaimedGbfsName: "Far Station", ClaimedDistanceMeters: 12000, Kind: "review-ref"),
        };

        await OsmFileFunctions.GenerateRefConflictOsmChangeFile(conflicts, TempSystem, SystemId);

        Assert.That(File.Exists(FileManager.GetSystemFullPath(TempSystem, OscFile)), Is.False);
    }

    [Test]
    public async Task GenerateRefConflictsFileAsync_WritesFixAndReviewLines()
    {
        var conflicts = new List<RefConflictDetector.RefConflict>
        {
            new RefConflictDetector.RefConflict(
                OsmNode(13795673649, 1, "McGill College / de Maisonneuve", 45.50265, -73.57254, refValue: null),
                null, Gbfs("73", "McGill College / de Maisonneuve", 45.50276, -73.57255), 12, null, null, "fix-ref"),
            new RefConflictDetector.RefConflict(
                OsmNode(777, 5, "Mackay / de Maisonneuve", 45.49684, -73.57923, refValue: "73"),
                "73", Gbfs("89", "Mackay / de Maisonneuve", 45.49681, -73.57911), 11,
                "McGill College / de Maisonneuve", 660, "review-ref"),
        };

        await GeoJsonGenerator.GenerateRefConflictsFileAsync(conflicts, TempSystem);

        var content = await FileManager.ReadSystemTextFileAsync(TempSystem, GeoFile);
        Assert.That(content, Does.Contain("\"action\":\"fix-ref\""));
        Assert.That(content, Does.Contain("\"resolvedRef\":\"73\""));
        Assert.That(content, Does.Contain("\"action\":\"review-ref\""));
        Assert.That(content, Does.Contain("\"currentRef\":\"73\""));
        Assert.That(content, Does.Contain("\"claimedGbfsName\":\"McGill College / de Maisonneuve\""));
        Assert.That(content, Does.Contain("\"likelyRef\":\"89\""));
    }
}
