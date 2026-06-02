using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System;

namespace prepareBikeParking.Tests;

public class BikeShareDataFetcherTests
{
    private class StubHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly HttpStatusCode _code;
        public StubHandler(string response, HttpStatusCode code = HttpStatusCode.OK)
        { _response = response; _code = code; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_code != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(_code){ Content = new StringContent("err") });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){ Content = new StringContent(_response) });
        }
    }

    // Routes responses by request URL substring so info and status feeds can return different bodies.
    private class RoutingHandler : HttpMessageHandler
    {
        private readonly List<(string match, string body, HttpStatusCode code)> _routes = new();
        public RoutingHandler Add(string match, string body, HttpStatusCode code = HttpStatusCode.OK)
        { _routes.Add((match, body, code)); return this; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            foreach (var (match, body, code) in _routes)
            {
                if (url.Contains(match))
                {
                    return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
                }
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("no route") });
        }
    }

    private class TestFactory : IHttpClientFactoryShim
    {
        private readonly HttpClient _client;
        public TestFactory(HttpMessageHandler handler)
        { _client = new HttpClient(handler); }
        public HttpClient CreateClient() => _client;
    }

    private string BuildFeed(params object[] stations)
    {
        var stationsJson = JsonSerializer.Serialize(stations);
        return $"{{\"data\":{{\"stations\":{stationsJson}}}}}";
    }

    [Test]
    public async Task Fetch_MinimalStation_Success()
    {
        var feed = BuildFeed(new { station_id = "1", name = "A", capacity = 5, lat = 43.1, lon = -79.2 });
        var fetcher = new BikeShareDataFetcher(new TestFactory(new StubHandler(feed)));
        var result = await fetcher.FetchFromApiAsync("http://example");
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].id, Is.EqualTo("1"));
    }

    [Test]
    public async Task Fetch_MissingCapacity_DefaultsZero()
    {
        var feed = BuildFeed(new { station_id = "2", name = "B", lat = 43.2, lon = -79.3 });
        var fetcher = new BikeShareDataFetcher(new TestFactory(new StubHandler(feed)));
        var result = await fetcher.FetchFromApiAsync("http://example");
        Assert.That(result[0].capacity, Is.EqualTo(0));
    }

    [Test]
    public void Fetch_MalformedJson_Throws()
    {
        var fetcher = new BikeShareDataFetcher(new TestFactory(new StubHandler("{ not json")));
        Assert.ThrowsAsync<System.Exception>((Func<Task>)(async () => await fetcher.FetchFromApiAsync("http://example")));
    }

    [Test]
    public void Fetch_NetworkFailure_WrapsException()
    {
        var fetcher = new BikeShareDataFetcher(new TestFactory(new StubHandler("{}", HttpStatusCode.InternalServerError)));
        Assert.ThrowsAsync<System.Exception>((Func<Task>)(async () => await fetcher.FetchFromApiAsync("http://example")));
    }

    private const string InfoUrl = "http://example/en/station_information.json";
    private const string StatusUrl = "http://example/en/station_status.json";

    private BikeShareDataFetcher FetcherWith(string infoBody, string? statusBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new RoutingHandler().Add("station_information", infoBody);
        if (statusBody != null) handler.Add("station_status", statusBody, statusCode);
        return new BikeShareDataFetcher(new TestFactory(handler));
    }

    [Test]
    public async Task Fetch_StatusWithBooleanFlags_MarksClosed()
    {
        var info = BuildFeed(
            new { station_id = "1", name = "Open", lat = 43.1, lon = -79.2 },
            new { station_id = "2", name = "OOS", lat = 43.2, lon = -79.3 },
            new { station_id = "3", name = "Decommissioned", lat = 43.3, lon = -79.4 });
        var status = BuildFeed(
            new { station_id = "1", is_installed = true, is_renting = true, is_returning = true },
            new { station_id = "2", is_installed = true, is_renting = false, is_returning = false },
            new { station_id = "3", is_installed = false, is_renting = false, is_returning = false });
        var fetcher = FetcherWith(info, status);

        var result = await fetcher.FetchFromApiAsync(InfoUrl, StatusUrl);

        Assert.That(result.Single(p => p.id == "1").IsClosed, Is.False);
        Assert.That(result.Single(p => p.id == "2").IsClosed, Is.True);
        Assert.That(result.Single(p => p.id == "3").IsClosed, Is.True);
    }

    [Test]
    public async Task Fetch_StatusWithIntegerFlags_MarksClosed()
    {
        var info = BuildFeed(
            new { station_id = "1", name = "Open", lat = 43.1, lon = -79.2 },
            new { station_id = "2", name = "OOS", lat = 43.2, lon = -79.3 });
        var status = BuildFeed(
            new { station_id = "1", is_installed = 1, is_renting = 1, is_returning = 1 },
            new { station_id = "2", is_installed = 1, is_renting = 0, is_returning = 0 });
        var fetcher = FetcherWith(info, status);

        var result = await fetcher.FetchFromApiAsync(InfoUrl, StatusUrl);

        Assert.That(result.Single(p => p.id == "1").IsClosed, Is.False);
        Assert.That(result.Single(p => p.id == "2").IsClosed, Is.True);
    }

    [Test]
    public async Task Fetch_StatusMissingRentingReturning_DefaultsToOpen()
    {
        var info = BuildFeed(new { station_id = "1", name = "A", lat = 43.1, lon = -79.2 });
        var status = BuildFeed(new { station_id = "1", is_installed = true });
        var fetcher = FetcherWith(info, status);

        var result = await fetcher.FetchFromApiAsync(InfoUrl, StatusUrl);

        Assert.That(result.Single(p => p.id == "1").IsClosed, Is.False);
    }

    [Test]
    public async Task Fetch_StatusFeed404_SoftFailsWithNoClosedFlags()
    {
        var info = BuildFeed(new { station_id = "1", name = "A", lat = 43.1, lon = -79.2 });
        var status = BuildFeed(new { station_id = "1", is_installed = false });
        var fetcher = FetcherWith(info, status, HttpStatusCode.NotFound);

        var result = await fetcher.FetchFromApiAsync(InfoUrl, StatusUrl);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].IsClosed, Is.False);
    }

    [Test]
    public async Task Fetch_StatusFeedEmptyStations_SoftFails()
    {
        var info = BuildFeed(new { station_id = "1", name = "A", lat = 43.1, lon = -79.2 });
        var status = "{\"data\":{}}";
        var fetcher = FetcherWith(info, status);

        var result = await fetcher.FetchFromApiAsync(InfoUrl, StatusUrl);

        Assert.That(result[0].IsClosed, Is.False);
    }

    [Test]
    public async Task Fetch_InfoStationWithoutStatusEntry_LeftOpen()
    {
        var info = BuildFeed(
            new { station_id = "1", name = "A", lat = 43.1, lon = -79.2 },
            new { station_id = "2", name = "B", lat = 43.2, lon = -79.3 });
        var status = BuildFeed(new { station_id = "1", is_installed = false });
        var fetcher = FetcherWith(info, status);

        var result = await fetcher.FetchFromApiAsync(InfoUrl, StatusUrl);

        Assert.That(result.Single(p => p.id == "1").IsClosed, Is.True);
        Assert.That(result.Single(p => p.id == "2").IsClosed, Is.False);
    }

    [Test]
    public async Task Fetch_StatusEntryWithoutInfoEntry_Ignored()
    {
        var info = BuildFeed(new { station_id = "1", name = "A", lat = 43.1, lon = -79.2 });
        var status = BuildFeed(
            new { station_id = "1", is_installed = true, is_renting = true, is_returning = true },
            new { station_id = "999", is_installed = false });
        var fetcher = FetcherWith(info, status);

        var result = await fetcher.FetchFromApiAsync(InfoUrl, StatusUrl);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].IsClosed, Is.False);
    }

    [Test]
    public async Task Fetch_NoStatusUrl_NothingFlagged()
    {
        var info = BuildFeed(new { station_id = "1", name = "A", lat = 43.1, lon = -79.2 });
        var fetcher = FetcherWith(info, null);

        var result = await fetcher.FetchFromApiAsync(InfoUrl);

        Assert.That(result[0].IsClosed, Is.False);
    }
}
