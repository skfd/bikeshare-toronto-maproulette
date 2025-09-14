using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

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
        Assert.ThrowsAsync<System.Exception>(async () => await fetcher.FetchFromApiAsync("http://example"));
    }

    [Test]
    public void Fetch_NetworkFailure_WrapsException()
    {
        var fetcher = new BikeShareDataFetcher(new TestFactory(new StubHandler("{}", HttpStatusCode.InternalServerError)));
        Assert.ThrowsAsync<System.Exception>(async () => await fetcher.FetchFromApiAsync("http://example"));
    }
}
