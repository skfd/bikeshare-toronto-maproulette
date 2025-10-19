using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System;

namespace prepareBikeParking.Tests;

public class OSMDataFetcherTests
{
    private class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode code,string body)> _responses;
        public StubHandler(IEnumerable<(HttpStatusCode code,string body)> responses)
        { _responses = new Queue<(HttpStatusCode,string)>(responses); }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var item = _responses.Count>0 ? _responses.Dequeue() : (HttpStatusCode.OK,"{\"elements\":[]}");
            return Task.FromResult(new HttpResponseMessage(item.Item1){ Content = new StringContent(item.Item2) });
        }
    }
    public class Factory : IOverpassHttpClientFactory
    {
        private readonly HttpClient _client;
        public Factory(HttpMessageHandler handler){ _client = new HttpClient(handler); }
        public HttpClient CreateClient() => _client;
    }

    private string WrapElements(params object[] elements)
    {
        var json = JsonSerializer.Serialize(elements);
        return $"{{\"elements\":{json}}}";
    }

    [Test]
    public async Task Parse_NodeDockingStation_WithRefAndName()
    {
    var node = new { type="node", id=1001, version=1, lat=43.1, lon=-79.2, tags=new { bicycle_rental="docking_station", name="Station A", @ref="S1", capacity="15" } };
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(node))})));
        var result = await fetcher.FetchFromOverpassApiAsync("TestSystemNode");
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].id, Is.EqualTo("S1"));
        Assert.That(result[0].capacity, Is.EqualTo(15));
    }

    [Test]
    public async Task Parse_NodeWithoutRef_UsesOsmPrefix()
    {
        var node = new { type="node", id=2002, version=1, lat=43.2, lon=-79.3, tags=new { bicycle_rental="docking_station", name="NoRef" } };
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(node))})));
        var result = await fetcher.FetchFromOverpassApiAsync("TestSystemNoRef");
        Assert.That(result[0].id, Is.EqualTo("osm_2002"));
    }

    [Test]
    public async Task Parse_Way_WithFirstNodeInline()
    {
    var way = new { type="way", id=3003, version=1, nodes=new[]{4004L}, tags=new { bicycle_rental="docking_station", name="WayStation", @ref="W1" } };
        var node = new { type="node", id=4004, version=1, lat=43.3, lon=-79.4, tags=new { bicycle_rental="docking_station" } };
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(node, way))})));
        var result = await fetcher.FetchFromOverpassApiAsync("TestWayInline");
        var w = result.Single(r=>r.id=="W1");
        Assert.That(w.lat, Is.EqualTo("43.3"));
    }

    [Test]
    public async Task Parse_Way_NeedsBatchNodeFetch()
    {
        var way = new { type="way", id=5005, version=1, nodes=new[]{6006L}, tags=new { bicycle_rental="docking_station", name="DeferredWay" } };
        // First response lacks node 6006 so triggers batch fetch (second response supplies it)
        var firstResponse = WrapElements(way);
        var node = new { type="node", id=6006, version=1, lat=43.6, lon=-79.6, tags=new { bicycle_rental="docking_station" } };
        var secondResponse = WrapElements(node);
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, firstResponse), (HttpStatusCode.OK, secondResponse)})));
        var result = await fetcher.FetchFromOverpassApiAsync("TestWayBatch");
    Assert.That(result.Any(), "Expected at least one parsed way after batch node fetch");
    }

    [Test]
    public async Task NonDockingStations_Ignored()
    {
        var node = new { type="node", id=7007, version=1, lat=43.7, lon=-79.7, tags=new { amenity="cafe" } };
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(node))})));
        var result = await fetcher.FetchFromOverpassApiAsync("TestIgnore");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task EnsureStationsOverpassFile_CreatesDefault()
    {
        var sys = "EdgeSys";
        var city = "EdgeCity";
        // Remove file if exists
        var path = prepareBikeParking.FileManager.GetSystemFullPath(sys, "stations.overpass");
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        await OSMDataFetcher.EnsureStationsOverpassFileAsync(sys, city);
        Assert.That(System.IO.File.Exists(path), Is.True);
        var text = System.IO.File.ReadAllText(path);
        Assert.That(text.Contains(city));
    }

    [Test]
    public async Task WayMissingNode_SkipsWithWarning()
    {
        var way = new { type="way", id=8008, version=1, nodes=new[]{9009L}, tags=new { bicycle_rental="docking_station", name="WayMissingNode" } };
        // No node 9009 in response, triggers skip
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(way))})));
        var result = await fetcher.FetchFromOverpassApiAsync("TestWayMissingNode");
        Assert.That(result.Any(r=>r.name=="WayMissingNode"), Is.False);
    }

    [Test]
    public void OverpassFailure_Throws()
    {
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.InternalServerError, "fail")})));
        Assert.ThrowsAsync<Exception>(async () => await fetcher.FetchFromOverpassApiAsync("FailSys"));
    }

    [Test]
    public async Task DuplicateRefValues_CreatesValidationReport()
    {
        // Create two nodes with the same ref value
        var node1 = new { type="node", id=1001, version=1, lat=43.1, lon=-79.2, tags=new { bicycle_rental="docking_station", name="Station A", @ref="DUP1" } };
        var node2 = new { type="node", id=1002, version=1, lat=43.2, lon=-79.3, tags=new { bicycle_rental="docking_station", name="Station B", @ref="DUP1" } };
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(node1, node2))})));

        var systemName = "TestDuplicates";
        var result = await fetcher.FetchFromOverpassApiAsync(systemName);

        // Both stations should be parsed
        Assert.That(result.Count, Is.EqualTo(2));

        // Check that the duplicate report file was created
        var duplicateReportPath = FileManager.GetSystemFullPath(systemName, "bikeshare_osm_duplicates.geojson");
        Assert.That(System.IO.File.Exists(duplicateReportPath), Is.True, "Duplicate validation report should be created");

        // Verify the report contains both duplicate stations
        var reportContent = await System.IO.File.ReadAllTextAsync(duplicateReportPath);
        Assert.That(reportContent.Contains("DUP1"), Is.True, "Report should contain duplicate ref value");
        Assert.That(reportContent.Contains("error"), Is.True, "Report should contain error property");
    }

    [Test]
    public async Task NoDuplicateRefValues_NoReportCreated()
    {
        // Create two nodes with different ref values
        var node1 = new { type="node", id=2001, version=1, lat=43.1, lon=-79.2, tags=new { bicycle_rental="docking_station", name="Station A", @ref="A1" } };
        var node2 = new { type="node", id=2002, version=1, lat=43.2, lon=-79.3, tags=new { bicycle_rental="docking_station", name="Station B", @ref="B1" } };
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(node1, node2))})));

        var systemName = "TestNoDuplicates";

        // Clean up any existing duplicate report
        var duplicateReportPath = FileManager.GetSystemFullPath(systemName, "bikeshare_osm_duplicates.geojson");
        if (System.IO.File.Exists(duplicateReportPath))
        {
            System.IO.File.Delete(duplicateReportPath);
        }

        var result = await fetcher.FetchFromOverpassApiAsync(systemName);

        // Both stations should be parsed
        Assert.That(result.Count, Is.EqualTo(2));

        // No duplicate report should be created
        Assert.That(System.IO.File.Exists(duplicateReportPath), Is.False, "No duplicate report should be created when there are no duplicates");
    }

    [Test]
    public async Task AutoGeneratedOsmIds_NotCheckedForDuplicates()
    {
        // Create two nodes without ref tags (will get auto-generated osm_XXX ids)
        var node1 = new { type="node", id=3001, version=1, lat=43.1, lon=-79.2, tags=new { bicycle_rental="docking_station", name="Station A" } };
        var node2 = new { type="node", id=3002, version=1, lat=43.2, lon=-79.3, tags=new { bicycle_rental="docking_station", name="Station B" } };
        var fetcher = new OSMDataFetcher(new Factory(new StubHandler(new[]{(HttpStatusCode.OK, WrapElements(node1, node2))})));

        var systemName = "TestAutoGenIds";

        // Clean up any existing duplicate report
        var duplicateReportPath = FileManager.GetSystemFullPath(systemName, "bikeshare_osm_duplicates.geojson");
        if (System.IO.File.Exists(duplicateReportPath))
        {
            System.IO.File.Delete(duplicateReportPath);
        }

        var result = await fetcher.FetchFromOverpassApiAsync(systemName);

        // Both stations should be parsed with auto-generated IDs
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result.All(r => r.id.StartsWith("osm_")), Is.True);

        // No duplicate report should be created for auto-generated IDs
        Assert.That(System.IO.File.Exists(duplicateReportPath), Is.False, "Auto-generated osm_ IDs should not trigger duplicate validation");
    }
}
